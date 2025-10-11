using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 热点追踪服务
    /// </summary>
    public class HotspotTrackingService
    {
        private readonly ILogger<HotspotTrackingService> _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly ContractInfoService _contractInfoService;
        private readonly TickerCacheService _tickerCacheService;
        private readonly SymbolInfoCacheService _symbolInfoCacheService;
        private readonly string _dataDirectory;
        
        // N天最高价缓存：symbol -> (highPrice, calculateTime, days)
        private readonly Dictionary<string, (decimal HighPrice, DateTime CalculateTime, int Days)> _nDayHighPriceCache = new();
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const int CacheExpiryHours = 1; // 缓存1小时后过期
        
        public HotspotTrackingService(
            ILogger<HotspotTrackingService> logger,
            IBinanceSimulatedApiClient apiClient,
            KlineDataStorageService klineStorageService,
            ContractInfoService contractInfoService,
            TickerCacheService tickerCacheService,
            SymbolInfoCacheService symbolInfoCacheService)
        {
            _logger = logger;
            _apiClient = apiClient;
            _klineStorageService = klineStorageService;
            _contractInfoService = contractInfoService;
            _tickerCacheService = tickerCacheService;
            _symbolInfoCacheService = symbolInfoCacheService;
            
            // 数据目录：AppData\Local\BinanceApps\HotspotTracking
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BinanceApps",
                "HotspotTracking"
            );
            
            Directory.CreateDirectory(_dataDirectory);
        }
        
        /// <summary>
        /// 扫描热点合约（多线程优化）
        /// </summary>
        public async Task<List<HotspotContract>> ScanHotspotContractsAsync(HotspotTrackingConfig config)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 1. 从缓存获取所有可交易的合约信息（过滤掉已下架的合约）
                    _logger.LogInformation("正在获取可交易合约列表...");
                    var allSymbols = await _symbolInfoCacheService.GetAllSymbolsInfoAsync().ConfigureAwait(false);
                    var tradingSymbols = new HashSet<string>();
                    
                    if (allSymbols != null && allSymbols.Count > 0)
                    {
                        tradingSymbols = allSymbols
                            .Where(s => s.IsTrading && s.QuoteAsset == "USDT" && s.ContractType == ContractType.Perpetual)
                            .Select(s => s.Symbol)
                            .ToHashSet();
                        _logger.LogInformation($"找到 {tradingSymbols.Count} 个可交易的USDT永续合约");
                    }
                    
                    // 2. 从缓存获取所有ticker数据
                    var allTickers = await _tickerCacheService.GetAllTickersAsync().ConfigureAwait(false);
                    _logger.LogInformation($"获取到 {allTickers.Count} 个合约的ticker数据");
                    
                    // 3. 只保留可交易的合约（过滤掉下架品种）
                    var tickers = allTickers;
                    if (tradingSymbols.Count > 0)
                    {
                        var originalCount = allTickers.Count;
                        tickers = allTickers.Where(t => tradingSymbols.Contains(t.Symbol)).ToList();
                        var filteredCount = originalCount - tickers.Count;
                        _logger.LogInformation($"过滤掉 {filteredCount} 个不可交易或非永续合约，剩余 {tickers.Count} 个");
                    }
                    
                    _logger.LogInformation($"开始扫描热点合约，ticker数量: {tickers.Count}");
                    
                    // 4. 使用并行处理（限制并发数为20）
                    var semaphore = new System.Threading.SemaphoreSlim(20);
                    var tasks = tickers.Select(async ticker =>
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            return await ProcessTickerAsync(ticker, config).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    var hotspots = results.Where(r => r != null).Select(r => r!).ToList();
                    
                    _logger.LogInformation($"扫描完成，发现 {hotspots.Count} 个热点合约");
                    return hotspots;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "扫描热点合约时发生错误");
                    return new List<HotspotContract>();
                }
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// 扫描量比异动和热点合约（返回两种数据）
        /// </summary>
        public async Task<(List<HotspotContract> VolumeAnomalyContracts, List<HotspotContract> RealtimeHotspots)> 
            ScanHotspotContractsWithAnomalyAsync(HotspotTrackingConfig config)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 1. 从缓存获取所有可交易的合约信息（过滤掉已下架的合约）
                    _logger.LogInformation("正在获取可交易合约列表...");
                    var allSymbols = await _symbolInfoCacheService.GetAllSymbolsInfoAsync().ConfigureAwait(false);
                    var tradingSymbols = new HashSet<string>();
                    
                    if (allSymbols != null && allSymbols.Count > 0)
                    {
                        tradingSymbols = allSymbols
                            .Where(s => s.IsTrading && s.QuoteAsset == "USDT" && s.ContractType == ContractType.Perpetual)
                            .Select(s => s.Symbol)
                            .ToHashSet();
                        _logger.LogInformation($"找到 {tradingSymbols.Count} 个可交易的USDT永续合约");
                    }
                    
                    // 2. 从缓存获取所有ticker数据
                    var allTickers = await _tickerCacheService.GetAllTickersAsync().ConfigureAwait(false);
                    _logger.LogInformation($"获取到 {allTickers.Count} 个合约的ticker数据");
                    
                    // 3. 只保留可交易的合约（过滤掉下架品种）
                    var tickers = allTickers;
                    if (tradingSymbols.Count > 0)
                    {
                        var originalCount = allTickers.Count;
                        tickers = allTickers.Where(t => tradingSymbols.Contains(t.Symbol)).ToList();
                        var filteredCount = originalCount - tickers.Count;
                        _logger.LogInformation($"过滤掉 {filteredCount} 个不可交易或非永续合约，剩余 {tickers.Count} 个");
                    }
                    
                    _logger.LogInformation($"开始扫描热点合约和量比异动，ticker数量: {tickers.Count}");
                    
                    // 4. 使用并行处理（限制并发数为20）
                    var semaphore = new System.Threading.SemaphoreSlim(20);
                    var tasks = tickers.Select(async ticker =>
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            return await ProcessTickerWithAnomalyAsync(ticker, config).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    
                    // 分离两种结果
                    var volumeAnomalies = new List<HotspotContract>();
                    var realtimeHotspots = new List<HotspotContract>();
                    
                    foreach (var (anomaly, hotspot) in results)
                    {
                        if (anomaly != null)
                            volumeAnomalies.Add(anomaly);
                        if (hotspot != null)
                            realtimeHotspots.Add(hotspot);
                    }
                    
                    _logger.LogInformation($"扫描完成，量比异动: {volumeAnomalies.Count} 个，实时热点: {realtimeHotspots.Count} 个");
                    return (volumeAnomalies, realtimeHotspots);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "扫描热点合约时发生错误");
                    return (new List<HotspotContract>(), new List<HotspotContract>());
                }
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// 处理单个ticker并返回量比异动和热点数据
        /// </summary>
        private async Task<(HotspotContract? VolumeAnomaly, HotspotContract? RealtimeHotspot)> 
            ProcessTickerWithAnomalyAsync(PriceStatistics ticker, HotspotTrackingConfig config)
        {
            try
            {
                // 1. 计算量比
                var contractInfo = _contractInfoService.GetContractInfo(ticker.Symbol);
                if (contractInfo == null || contractInfo.CirculatingSupply <= 0)
                    return (null, null);
                
                var circulatingMarketCap = contractInfo.CirculatingSupply * ticker.LastPrice;
                if (circulatingMarketCap <= 0)
                    return (null, null);
                
                // 计算发行总市值
                var totalMarketCap = contractInfo.TotalSupply * ticker.LastPrice;
                
                // 计算流通率（百分比）
                var circulatingRate = contractInfo.TotalSupply > 0 
                    ? (contractInfo.CirculatingSupply / contractInfo.TotalSupply) * 100m 
                    : 0m;
                
                // 转换为万单位
                var circulatingMarketCapInWan = circulatingMarketCap / 10_000m;
                
                // 流通市值过滤（单位：万）
                if (circulatingMarketCapInWan < config.MinCirculatingMarketCap || 
                    circulatingMarketCapInWan > config.MaxCirculatingMarketCap)
                    return (null, null);
                
                var volumeRatio = (ticker.QuoteVolume / circulatingMarketCap) * 100m; // 转为百分比
                
                // 2. 检查量比是否超过阈值
                if (volumeRatio < config.VolumeRatioThreshold)
                    return (null, null);
                
                // 3. 创建量比异动对象（只要量比超阈值）
                var volumeAnomaly = new HotspotContract
                {
                    Symbol = ticker.Symbol,
                    LastPrice = ticker.LastPrice,
                    PriceChangePercent24h = ticker.PriceChangePercent,
                    QuoteVolume24h = ticker.QuoteVolume,
                    VolumeRatio = volumeRatio,
                    CirculatingMarketCap = circulatingMarketCap,
                    TotalMarketCap = totalMarketCap,
                    CirculatingRate = circulatingRate
                };
                
                // 4. 计算N天最高价（用于判断是否是实时热点）
                var highPriceNDays = await CalculateNDayHighPriceAsync(ticker.Symbol, config.HighPriceDays).ConfigureAwait(false);
                if (!highPriceNDays.HasValue)
                    return (volumeAnomaly, null); // 只返回量比异动
                
                // 5. 检查是否超过N天最高价（实时热点的额外条件）
                if (ticker.LastPrice <= highPriceNDays.Value)
                    return (volumeAnomaly, null); // 只返回量比异动
                
                // 6. 计算相对N天最高价的涨幅
                var priceChangeFromHigh = ((ticker.LastPrice - highPriceNDays.Value) / highPriceNDays.Value) * 100m;
                
                // 7. 创建实时热点对象（量比超阈值且超N天最高）
                var realtimeHotspot = new HotspotContract
                {
                    Symbol = ticker.Symbol,
                    LastPrice = ticker.LastPrice,
                    PriceChangePercent24h = ticker.PriceChangePercent,
                    QuoteVolume24h = ticker.QuoteVolume,
                    VolumeRatio = volumeRatio,
                    HighPriceNDays = highPriceNDays.Value,
                    PriceChangeFromNDayHigh = priceChangeFromHigh,
                    CirculatingMarketCap = circulatingMarketCap,
                    TotalMarketCap = totalMarketCap,
                    CirculatingRate = circulatingRate
                };
                
                return (volumeAnomaly, realtimeHotspot);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"处理合约 {ticker.Symbol} 时出错: {ex.Message}");
                return (null, null);
            }
        }
        
        /// <summary>
        /// 处理单个ticker（提取为独立方法以支持并行）
        /// </summary>
        private async Task<HotspotContract?> ProcessTickerAsync(PriceStatistics ticker, HotspotTrackingConfig config)
        {
            try
            {
                // 1. 计算量比
                var contractInfo = _contractInfoService.GetContractInfo(ticker.Symbol);
                if (contractInfo == null || contractInfo.CirculatingSupply <= 0)
                    return null;
                
                var circulatingMarketCap = contractInfo.CirculatingSupply * ticker.LastPrice;
                if (circulatingMarketCap <= 0)
                    return null;
                
                // 计算发行总市值
                var totalMarketCap = contractInfo.TotalSupply * ticker.LastPrice;
                
                // 计算流通率（百分比）
                var circulatingRate = contractInfo.TotalSupply > 0 
                    ? (contractInfo.CirculatingSupply / contractInfo.TotalSupply) * 100m 
                    : 0m;
                
                // 转换为万单位
                var circulatingMarketCapInWan = circulatingMarketCap / 10_000m;
                
                // 流通市值过滤（单位：万）
                if (circulatingMarketCapInWan < config.MinCirculatingMarketCap || 
                    circulatingMarketCapInWan > config.MaxCirculatingMarketCap)
                    return null;
                
                var volumeRatio = (ticker.QuoteVolume / circulatingMarketCap) * 100m; // 转为百分比
                
                // 2. 检查量比是否超过阈值
                if (volumeRatio < config.VolumeRatioThreshold)
                    return null;
                
                // 3. 计算N天最高价（从昨天开始往前N天）
                var highPriceNDays = await CalculateNDayHighPriceAsync(ticker.Symbol, config.HighPriceDays).ConfigureAwait(false);
                if (!highPriceNDays.HasValue)
                    return null;
                
                // 4. 检查是否超过N天最高价
                if (ticker.LastPrice <= highPriceNDays.Value)
                    return null;
                
                // 5. 计算相对N天最高价的涨幅
                var priceChangeFromHigh = ((ticker.LastPrice - highPriceNDays.Value) / highPriceNDays.Value) * 100m;
                
                // 6. 创建热点合约对象
                return new HotspotContract
                {
                    Symbol = ticker.Symbol,
                    LastPrice = ticker.LastPrice,
                    PriceChangePercent24h = ticker.PriceChangePercent,
                    QuoteVolume24h = ticker.QuoteVolume,
                    VolumeRatio = volumeRatio,
                    HighPriceNDays = highPriceNDays.Value,
                    PriceChangeFromNDayHigh = priceChangeFromHigh,
                    CirculatingMarketCap = circulatingMarketCap,
                    TotalMarketCap = totalMarketCap,
                    CirculatingRate = circulatingRate
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"处理合约 {ticker.Symbol} 时出错: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 计算N天最高价（从昨天开始往前N天）- 带缓存优化
        /// </summary>
        private async Task<decimal?> CalculateNDayHighPriceAsync(string symbol, int days)
        {
            // 1. 检查缓存
            await _cacheLock.WaitAsync();
            try
            {
                if (_nDayHighPriceCache.TryGetValue(symbol, out var cached))
                {
                    var cacheAge = (DateTime.Now - cached.CalculateTime).TotalHours;
                    var isSameDay = cached.CalculateTime.Date == DateTime.Today;
                    
                    // 如果是同一天计算的，且天数相同，且未过期（1小时内），使用缓存
                    if (cached.Days == days && isSameDay && cacheAge < CacheExpiryHours)
                    {
                        _logger.LogDebug($"{symbol} 使用缓存的{days}天最高价: {cached.HighPrice}, 缓存年龄: {cacheAge:F1}小时");
                        return cached.HighPrice;
                    }
                }
            }
            finally
            {
                _cacheLock.Release();
            }
            
            // 2. 缓存无效，重新计算
            try
            {
                var (klines, success, error) = await _klineStorageService.LoadKlineDataAsync(symbol);
                if (!success || klines == null || klines.Count == 0)
                    return null;
                
                // 从昨天开始往前N天
                var endDate = DateTime.Today; // 今天0点（不包含今天）
                var startDate = endDate.AddDays(-days);
                
                var relevantKlines = klines
                    .Where(k => k.OpenTime >= startDate && k.OpenTime < endDate)
                    .ToList();
                
                if (relevantKlines.Count == 0)
                    return null;
                
                var highPrice = relevantKlines.Max(k => k.HighPrice);
                
                // 3. 更新缓存
                await _cacheLock.WaitAsync();
                try
                {
                    _nDayHighPriceCache[symbol] = (highPrice, DateTime.Now, days);
                    _logger.LogDebug($"{symbol} 计算并缓存{days}天最高价: {highPrice}");
                }
                finally
                {
                    _cacheLock.Release();
                }
                
                return highPrice;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"计算 {symbol} 的N天最高价时出错: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 清除N天最高价缓存（用户可以手动调用以强制刷新）
        /// </summary>
        public async Task ClearNDayHighPriceCacheAsync()
        {
            await _cacheLock.WaitAsync();
            try
            {
                var count = _nDayHighPriceCache.Count;
                _nDayHighPriceCache.Clear();
                _logger.LogInformation($"已清除{count}个合约的N天最高价缓存");
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        
        /// <summary>
        /// 获取缓存状态
        /// </summary>
        public (int CachedCount, DateTime? OldestCacheTime) GetNDayHighPriceCacheStatus()
        {
            if (_nDayHighPriceCache.Count == 0)
                return (0, null);
                
            var oldestTime = _nDayHighPriceCache.Values.Min(v => v.CalculateTime);
            return (_nDayHighPriceCache.Count, oldestTime);
        }
        
        /// <summary>
        /// 更新缓存区数据（同步方法，供后台线程调用）
        /// </summary>
        public async Task UpdateCachedContractsAsync(
            List<HotspotContract> currentHotspots,
            Dictionary<string, CachedHotspotContract> cachedContracts,
            HotspotTrackingConfig config)
        {
            // 1. 添加新的热点合约到缓存区
            foreach (var hotspot in currentHotspots)
            {
                if (cachedContracts.ContainsKey(hotspot.Symbol))
                {
                    // 已存在：重置倒计时，更新数据
                    var cached = cachedContracts[hotspot.Symbol];
                    cached.LastPrice = hotspot.LastPrice;
                    cached.PriceChangePercent24h = hotspot.PriceChangePercent24h;
                    cached.QuoteVolume24h = hotspot.QuoteVolume24h;
                    cached.VolumeRatio = hotspot.VolumeRatio;
                    cached.HighPriceNDays = hotspot.HighPriceNDays;
                    cached.PriceChangeFromNDayHigh = hotspot.PriceChangeFromNDayHigh;
                    cached.CirculatingMarketCap = hotspot.CirculatingMarketCap;
                    cached.TotalMarketCap = hotspot.TotalMarketCap;
                    cached.CirculatingRate = hotspot.CirculatingRate;
                    
                    // 重置倒计时
                    var now = DateTime.Now;
                    cached.CountdownStartTime = now;
                    cached.ExpiryTime = now.AddHours(config.CacheExpiryHours);
                    
                    // 更新录入后最高价
                    if (hotspot.LastPrice > cached.HighestPriceAfterEntry)
                    {
                        cached.HighestPriceAfterEntry = hotspot.LastPrice;
                    }
                    
                    _logger.LogDebug($"更新缓存合约: {hotspot.Symbol}, 重置倒计时");
                }
                else
                {
                    // 新合约：添加到缓存区
                    var now = DateTime.Now;
                    var cached = new CachedHotspotContract
                    {
                        Symbol = hotspot.Symbol,
                        LastPrice = hotspot.LastPrice,
                        PriceChangePercent24h = hotspot.PriceChangePercent24h,
                        QuoteVolume24h = hotspot.QuoteVolume24h,
                        VolumeRatio = hotspot.VolumeRatio,
                        HighPriceNDays = hotspot.HighPriceNDays,
                        PriceChangeFromNDayHigh = hotspot.PriceChangeFromNDayHigh,
                        CirculatingMarketCap = hotspot.CirculatingMarketCap,
                        TotalMarketCap = hotspot.TotalMarketCap,
                        CirculatingRate = hotspot.CirculatingRate,
                        EntryTime = now,
                        EntryPrice = hotspot.LastPrice,
                        EntryNDayHighPrice = hotspot.HighPriceNDays, // 保存录入时的N天最高价
                        HighestPriceAfterEntry = hotspot.LastPrice,
                        CountdownStartTime = now,
                        ExpiryTime = now.AddHours(config.CacheExpiryHours)
                    };
                    
                    cachedContracts[hotspot.Symbol] = cached;
                    _logger.LogInformation($"新增热点合约到缓存: {hotspot.Symbol}");
                }
            }
            
            // 2. 批量更新不在实时监控区的合约数据
            var currentSymbols = new HashSet<string>(currentHotspots.Select(h => h.Symbol));
            var contractsToUpdate = cachedContracts.Values
                .Where(c => !currentSymbols.Contains(c.Symbol))
                .ToList();
            
            if (contractsToUpdate.Any())
            {
                // 一次性从缓存获取所有ticker数据，避免重复请求
                try
                {
                    var tickers = await _tickerCacheService.GetAllTickersAsync().ConfigureAwait(false);
                    var tickerDict = tickers.ToDictionary(t => t.Symbol);
                    
                    // 批量更新价格
                    foreach (var cached in contractsToUpdate)
                    {
                        if (tickerDict.TryGetValue(cached.Symbol, out var ticker))
                        {
                            cached.LastPrice = ticker.LastPrice;
                            cached.PriceChangePercent24h = ticker.PriceChangePercent;
                            cached.QuoteVolume24h = ticker.QuoteVolume;
                            
                            // 更新录入后最高价
                            if (ticker.LastPrice > cached.HighestPriceAfterEntry)
                            {
                                cached.HighestPriceAfterEntry = ticker.LastPrice;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "批量更新缓存合约价格失败");
                }
            }
        }
        
        
        /// <summary>
        /// 清理过期缓存，移动到回收区
        /// </summary>
        public void CleanExpiredCache(
            Dictionary<string, CachedHotspotContract> cachedContracts,
            Dictionary<string, RecycledHotspotContract> recycledContracts)
        {
            var expiredSymbols = cachedContracts
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var symbol in expiredSymbols)
            {
                var cached = cachedContracts[symbol];
                
                // 移动到回收区
                var recycled = new RecycledHotspotContract
                {
                    Symbol = cached.Symbol,
                    LastPrice = cached.LastPrice,
                    PriceChangePercent24h = cached.PriceChangePercent24h,
                    QuoteVolume24h = cached.QuoteVolume24h,
                    VolumeRatio = cached.VolumeRatio,
                    HighPriceNDays = cached.HighPriceNDays,
                    PriceChangeFromNDayHigh = cached.PriceChangeFromNDayHigh,
                    CirculatingMarketCap = cached.CirculatingMarketCap,
                    TotalMarketCap = cached.TotalMarketCap,
                    CirculatingRate = cached.CirculatingRate,
                    EntryTime = cached.EntryTime,
                    EntryPrice = cached.EntryPrice,
                    HighestPriceAfterEntry = cached.HighestPriceAfterEntry,
                    ExpiryTime = cached.ExpiryTime,
                    RecycleTime = DateTime.Now,
                    RecycleExpiryTime = DateTime.Now.AddDays(3)
                };
                
                recycledContracts[symbol] = recycled;
                cachedContracts.Remove(symbol);
                
                _logger.LogInformation($"合约过期，移至回收区: {symbol}");
            }
        }
        
        /// <summary>
        /// 清理回收区过期数据
        /// </summary>
        public void CleanRecycledContracts(Dictionary<string, RecycledHotspotContract> recycledContracts)
        {
            var toDelete = recycledContracts
                .Where(kvp => kvp.Value.ShouldDelete)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var symbol in toDelete)
            {
                recycledContracts.Remove(symbol);
                _logger.LogInformation($"从回收区删除: {symbol}");
            }
        }
        
        /// <summary>
        /// 保存数据到本地
        /// </summary>
        public async Task SaveDataAsync(string instanceId, HotspotTrackingData data)
        {
            try
            {
                // 确保目录存在
                if (!Directory.Exists(_dataDirectory))
                {
                    Directory.CreateDirectory(_dataDirectory);
                    _logger.LogInformation($"创建数据目录: {_dataDirectory}");
                }
                
                var filePath = Path.Combine(_dataDirectory, $"hotspot_tracking_{instanceId}.json");
                
                data.LastSaveTime = DateTime.Now;
                
                // 打印保存前的数据快照（用于调试）
                if (data.CachedContracts.Count > 0)
                {
                    var firstContract = data.CachedContracts.First().Value;
                    _logger.LogInformation($"📋 保存前数据快照:");
                    _logger.LogInformation($"   第一个合约: {firstContract.Symbol}");
                    _logger.LogInformation($"   录入时间: {firstContract.EntryTime:yyyy-MM-dd HH:mm:ss}");
                    _logger.LogInformation($"   倒计时开始: {firstContract.CountdownStartTime:yyyy-MM-dd HH:mm:ss}");
                    _logger.LogInformation($"   到期时间: {firstContract.ExpiryTime:yyyy-MM-dd HH:mm:ss}");
                }
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNameCaseInsensitive = true
                };
                
                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json);
                
                _logger.LogInformation($"✅ 热点追踪数据已保存: {filePath}");
                _logger.LogInformation($"   缓存区合约: {data.CachedContracts.Count}个");
                _logger.LogInformation($"   回收区合约: {data.RecycledContracts.Count}个");
                _logger.LogInformation($"   文件大小: {new FileInfo(filePath).Length / 1024.0:F2} KB");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 保存热点追踪数据失败: {instanceId}");
                throw;
            }
        }
        
        /// <summary>
        /// 从本地加载数据
        /// </summary>
        public async Task<HotspotTrackingData?> LoadDataAsync(string instanceId)
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, $"hotspot_tracking_{instanceId}.json");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation($"📁 未找到历史数据文件: {filePath}");
                    return null;
                }
                
                var json = await File.ReadAllTextAsync(filePath);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var data = JsonSerializer.Deserialize<HotspotTrackingData>(json, options);
                
                if (data != null)
                {
                    _logger.LogInformation($"✅ 加载热点追踪数据: {filePath}");
                    _logger.LogInformation($"   缓存区合约: {data.CachedContracts.Count}个");
                    _logger.LogInformation($"   回收区合约: {data.RecycledContracts.Count}个");
                    _logger.LogInformation($"   最后保存时间: {data.LastSaveTime:yyyy-MM-dd HH:mm:ss}");
                    
                    // 打印加载后的数据快照（用于调试）
                    if (data.CachedContracts.Count > 0)
                    {
                        var firstContract = data.CachedContracts.First().Value;
                        _logger.LogInformation($"📋 加载后数据快照:");
                        _logger.LogInformation($"   第一个合约: {firstContract.Symbol}");
                        _logger.LogInformation($"   录入时间: {firstContract.EntryTime:yyyy-MM-dd HH:mm:ss}");
                        _logger.LogInformation($"   倒计时开始: {firstContract.CountdownStartTime:yyyy-MM-dd HH:mm:ss}");
                        _logger.LogInformation($"   到期时间: {firstContract.ExpiryTime:yyyy-MM-dd HH:mm:ss}");
                        _logger.LogInformation($"   剩余时间: {firstContract.RemainingCacheHours:F1}小时");
                    }
                }
                
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ 加载热点追踪数据失败: {instanceId}");
                return null;
            }
        }
    }
}

