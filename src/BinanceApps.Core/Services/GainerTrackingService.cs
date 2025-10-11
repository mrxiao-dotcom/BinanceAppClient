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
    /// 涨幅榜追踪服务
    /// </summary>
    public class GainerTrackingService
    {
        private readonly ILogger<GainerTrackingService> _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly ContractInfoService _contractInfoService;
        private readonly TickerCacheService _tickerCacheService;
        private readonly SymbolInfoCacheService _symbolInfoCacheService;
        private readonly string _dataDirectory;
        
        // N天最低价缓存：symbol -> (lowPrice, calculateTime, days)
        private readonly Dictionary<string, (decimal LowPrice, DateTime CalculateTime, int Days)> _nDayLowPriceCache = new();
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);
        private const int CacheExpiryHours = 1; // 缓存1小时后过期
        
        public GainerTrackingService(
            ILogger<GainerTrackingService> logger,
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
            
            // 数据目录：AppData\Local\BinanceApps\GainerTracking
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BinanceApps",
                "GainerTracking"
            );
            
            Directory.CreateDirectory(_dataDirectory);
        }
        
        /// <summary>
        /// 扫描N天涨幅榜（多线程优化）
        /// </summary>
        public async Task<List<GainerContract>> ScanTopGainersAsync(GainerTrackingConfig config)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // 1. 从缓存获取可交易的合约列表
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
                    
                    // 2. 从缓存获取ticker数据
                    var allTickers = await _tickerCacheService.GetAllTickersAsync().ConfigureAwait(false);
                    var tickers = allTickers;
                    if (tradingSymbols.Count > 0)
                    {
                        tickers = allTickers.Where(t => tradingSymbols.Contains(t.Symbol)).ToList();
                    }
                    
                    _logger.LogInformation($"开始计算{config.NDays}天涨幅，ticker数量: {tickers.Count}");
                    
                    // 3. 并行计算N天涨幅
                    var semaphore = new SemaphoreSlim(20);
                    var tasks = tickers.Select(async ticker =>
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            return await CalculateNDayGainAsync(ticker, config).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    var gainers = results.Where(r => r != null).Select(r => r!).ToList();
                    
                    // 4. 按涨幅排序，取前N名
                    var topGainers = gainers
                        .OrderByDescending(g => g.NDayGainPercent)
                        .Take(config.TopCount)
                        .ToList();
                    
                    // 5. 设置排名
                    for (int i = 0; i < topGainers.Count; i++)
                    {
                        topGainers[i].Rank = i + 1;
                    }
                    
                    _logger.LogInformation($"扫描完成，涨幅榜前{config.TopCount}名");
                    return topGainers;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "扫描涨幅榜时发生错误");
                    return new List<GainerContract>();
                }
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// 计算单个合约的N天涨幅（基于N天内最低价）
        /// </summary>
        private async Task<GainerContract?> CalculateNDayGainAsync(PriceStatistics ticker, GainerTrackingConfig config)
        {
            try
            {
                // 1. 尝试从缓存获取N天最低价
                decimal lowestPrice;
                
                await _cacheLock.WaitAsync();
                try
                {
                    if (_nDayLowPriceCache.TryGetValue(ticker.Symbol, out var cached))
                    {
                        var cacheAge = (DateTime.Now - cached.CalculateTime).TotalHours;
                        var isSameDay = cached.CalculateTime.Date == DateTime.Today;
                        
                        // 如果是同一天计算的，且天数相同，且未过期，使用缓存
                        if (cached.Days == config.NDays && isSameDay && cacheAge < CacheExpiryHours)
                        {
                            lowestPrice = cached.LowPrice;
                            _logger.LogDebug($"{ticker.Symbol} 使用缓存的{config.NDays}天最低价: {lowestPrice}");
                            goto CalculateGain; // 跳到计算涨幅部分
                        }
                    }
                }
                finally
                {
                    _cacheLock.Release();
                }
                
                // 2. 缓存无效，从K线数据计算
                var (klines, success, error) = await _klineStorageService.LoadKlineDataAsync(ticker.Symbol);
                if (!success || klines == null || klines.Count == 0)
                    return null;
                
                // 3. 获取N天内的K线数据（包括今天）
                var startDate = DateTime.Today.AddDays(-config.NDays + 1); // 包括今天，所以是N-1天前
                var endDate = DateTime.Today.AddDays(1); // 不包含明天
                
                var nDayKlines = klines
                    .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date < endDate.Date)
                    .ToList();
                
                if (nDayKlines.Count == 0)
                    return null;
                
                // 4. 找到N天内的最低价
                lowestPrice = nDayKlines.Min(k => k.LowPrice);
                if (lowestPrice <= 0)
                    return null;
                
                // 5. 更新缓存
                await _cacheLock.WaitAsync();
                try
                {
                    _nDayLowPriceCache[ticker.Symbol] = (lowestPrice, DateTime.Now, config.NDays);
                    _logger.LogDebug($"{ticker.Symbol} 计算并缓存{config.NDays}天最低价: {lowestPrice}");
                }
                finally
                {
                    _cacheLock.Release();
                }
                
            CalculateGain:
                
                // 4. 计算最新价相对于最低价的涨幅
                var gainPercent = ((ticker.LastPrice - lowestPrice) / lowestPrice) * 100m;
                
                // 5. 获取流通市值
                var contractInfo = _contractInfoService.GetContractInfo(ticker.Symbol);
                var circulatingMarketCap = 0m;
                if (contractInfo != null && contractInfo.CirculatingSupply > 0)
                {
                    circulatingMarketCap = contractInfo.CirculatingSupply * ticker.LastPrice;
                }
                
                return new GainerContract
                {
                    Symbol = ticker.Symbol,
                    LastPrice = ticker.LastPrice,
                    NDayAgoPrice = lowestPrice, // 存储N天最低价
                    NDayGainPercent = gainPercent,
                    PriceChangePercent24h = ticker.PriceChangePercent,
                    QuoteVolume24h = ticker.QuoteVolume,
                    CirculatingMarketCap = circulatingMarketCap
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"计算 {ticker.Symbol} 的{config.NDays}天涨幅时出错: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 更新缓存区数据
        /// </summary>
        public async Task UpdateCachedContractsAsync(
            List<GainerContract> topGainers,
            Dictionary<string, CachedGainerContract> cachedContracts,
            GainerTrackingConfig config)
        {
            // 1. 添加新进榜的合约到缓存区
            foreach (var gainer in topGainers)
            {
                if (cachedContracts.ContainsKey(gainer.Symbol))
                {
                    // 已存在：重置倒计时，更新数据
                    var cached = cachedContracts[gainer.Symbol];
                    cached.LastPrice = gainer.LastPrice;
                    cached.NDayAgoPrice = gainer.NDayAgoPrice; // N天最低价
                    cached.NDayGainPercent = gainer.NDayGainPercent;
                    cached.PriceChangePercent24h = gainer.PriceChangePercent24h;
                    cached.QuoteVolume24h = gainer.QuoteVolume24h;
                    cached.CirculatingMarketCap = gainer.CirculatingMarketCap;
                    cached.Rank = gainer.Rank;
                    
                    // 重置倒计时
                    var now = DateTime.Now;
                    cached.CountdownStartTime = now;
                    cached.ExpiryTime = now.AddHours(config.CacheExpiryHours);
                    
                    // 更新录入后最高价
                    if (gainer.LastPrice > cached.HighestPriceAfterEntry)
                    {
                        cached.HighestPriceAfterEntry = gainer.LastPrice;
                    }
                    
                    _logger.LogDebug($"更新涨幅榜缓存: {gainer.Symbol}, 排名{gainer.Rank}, 重置倒计时");
                }
                else
                {
                    // 新合约：添加到缓存区
                    var now = DateTime.Now;
                    var cached = new CachedGainerContract
                    {
                        Symbol = gainer.Symbol,
                        LastPrice = gainer.LastPrice,
                        NDayAgoPrice = gainer.NDayAgoPrice,
                        NDayGainPercent = gainer.NDayGainPercent,
                        PriceChangePercent24h = gainer.PriceChangePercent24h,
                        QuoteVolume24h = gainer.QuoteVolume24h,
                        CirculatingMarketCap = gainer.CirculatingMarketCap,
                        Rank = gainer.Rank,
                        EntryTime = now,
                        EntryPrice = gainer.LastPrice,
                        EntryRank = gainer.Rank,
                        HighestPriceAfterEntry = gainer.LastPrice,
                        CountdownStartTime = now,
                        ExpiryTime = now.AddHours(config.CacheExpiryHours)
                    };
                    
                    cachedContracts[gainer.Symbol] = cached;
                    _logger.LogInformation($"新增涨幅榜合约到缓存: {gainer.Symbol}, 排名{gainer.Rank}");
                }
            }
            
            // 2. 批量更新不在榜单上的合约数据
            var currentSymbols = new HashSet<string>(topGainers.Select(g => g.Symbol));
            var contractsToUpdate = cachedContracts.Values
                .Where(c => !currentSymbols.Contains(c.Symbol))
                .ToList();
            
            if (contractsToUpdate.Any())
            {
                try
                {
                    var tickers = await _tickerCacheService.GetAllTickersAsync().ConfigureAwait(false);
                    var tickerDict = tickers.ToDictionary(t => t.Symbol);
                    
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
                    _logger.LogWarning(ex, "批量更新涨幅榜缓存合约价格失败");
                }
            }
        }
        
        /// <summary>
        /// 清理过期缓存，移动到回收区
        /// </summary>
        public void CleanExpiredCache(
            Dictionary<string, CachedGainerContract> cachedContracts,
            Dictionary<string, RecycledGainerContract> recycledContracts)
        {
            var expiredSymbols = cachedContracts
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var symbol in expiredSymbols)
            {
                var cached = cachedContracts[symbol];
                
                // 移动到回收区
                var recycled = new RecycledGainerContract
                {
                    Symbol = cached.Symbol,
                    LastPrice = cached.LastPrice,
                    NDayAgoPrice = cached.NDayAgoPrice,
                    NDayGainPercent = cached.NDayGainPercent,
                    PriceChangePercent24h = cached.PriceChangePercent24h,
                    QuoteVolume24h = cached.QuoteVolume24h,
                    CirculatingMarketCap = cached.CirculatingMarketCap,
                    Rank = cached.Rank,
                    RecycleTime = DateTime.Now,
                    CachedDurationHours = cached.CachedDurationHours
                };
                
                recycledContracts[symbol] = recycled;
                cachedContracts.Remove(symbol);
                
                _logger.LogInformation($"涨幅榜合约过期并移至回收区: {symbol}");
            }
        }
        
        /// <summary>
        /// 清理回收区（保留3天）
        /// </summary>
        public void CleanRecycledContracts(Dictionary<string, RecycledGainerContract> recycledContracts)
        {
            var threeDaysAgo = DateTime.Now.AddDays(-3);
            var toRemove = recycledContracts
                .Where(kvp => kvp.Value.RecycleTime < threeDaysAgo)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var symbol in toRemove)
            {
                recycledContracts.Remove(symbol);
                _logger.LogDebug($"从回收区移除3天前的合约: {symbol}");
            }
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        public async Task SaveDataAsync(string instanceId, GainerTrackingData data)
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, $"{instanceId}.json");
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNameCaseInsensitive = true
                };
                
                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
                
                _logger.LogDebug($"涨幅榜数据已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存涨幅榜数据失败");
            }
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        public async Task<GainerTrackingData?> LoadDataAsync(string instanceId)
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, $"{instanceId}.json");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation($"涨幅榜数据文件不存在: {filePath}");
                    return null;
                }
                
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var data = JsonSerializer.Deserialize<GainerTrackingData>(json, options);
                
                if (data != null)
                {
                    _logger.LogInformation($"成功加载涨幅榜数据: 缓存={data.CachedContracts.Count}, 回收={data.RecycledContracts.Count}");
                }
                
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载涨幅榜数据失败");
                return null;
            }
        }
    }
}

