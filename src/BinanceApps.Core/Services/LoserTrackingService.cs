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
    /// 跌幅榜追踪服务
    /// </summary>
    public class LoserTrackingService
    {
        private readonly ILogger<LoserTrackingService> _logger;
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
        
        public LoserTrackingService(
            ILogger<LoserTrackingService> logger,
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
            
            // 数据目录：AppData\Local\BinanceApps\LoserTracking
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BinanceApps",
                "LoserTracking"
            );
            
            Directory.CreateDirectory(_dataDirectory);
        }
        
        /// <summary>
        /// 扫描N天跌幅榜（多线程优化）
        /// </summary>
        public async Task<List<LoserContract>> ScanTopLosersAsync(LoserTrackingConfig config)
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
                    
                    _logger.LogInformation($"开始计算{config.NDays}天跌幅，ticker数量: {tickers.Count}");
                    
                    // 3. 并行计算N天跌幅
                    var semaphore = new SemaphoreSlim(20);
                    var tasks = tickers.Select(async ticker =>
                    {
                        await semaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            return await CalculateNDayLossAsync(ticker, config).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                    var losers = results.Where(r => r != null).Select(r => r!).ToList();
                    
                    // 4. 按跌幅排序（跌幅从大到小，即最负的值排在前面），取前N名
                    var topLosers = losers
                        .OrderBy(l => l.NDayLossPercent) // 升序，因为跌幅是负值
                        .Take(config.TopCount)
                        .ToList();
                    
                    // 5. 设置排名
                    for (int i = 0; i < topLosers.Count; i++)
                    {
                        topLosers[i].Rank = i + 1;
                    }
                    
                    _logger.LogInformation($"扫描完成，跌幅榜前{config.TopCount}名");
                    return topLosers;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "扫描跌幅榜时发生错误");
                    return new List<LoserContract>();
                }
            }).ConfigureAwait(false);
        }
        
        /// <summary>
        /// 计算单个合约的N天跌幅（基于N天内最高价）
        /// </summary>
        private async Task<LoserContract?> CalculateNDayLossAsync(PriceStatistics ticker, LoserTrackingConfig config)
        {
            try
            {
                // 1. 尝试从缓存获取N天最高价
                decimal highestPrice;
                
                await _cacheLock.WaitAsync();
                try
                {
                    if (_nDayHighPriceCache.TryGetValue(ticker.Symbol, out var cached))
                    {
                        var cacheAge = (DateTime.Now - cached.CalculateTime).TotalHours;
                        var isSameDay = cached.CalculateTime.Date == DateTime.Today;
                        
                        // 如果是同一天计算的，且天数相同，且未过期，使用缓存
                        if (cached.Days == config.NDays && isSameDay && cacheAge < CacheExpiryHours)
                        {
                            highestPrice = cached.HighPrice;
                            _logger.LogDebug($"{ticker.Symbol} 使用缓存的{config.NDays}天最高价: {highestPrice}");
                            goto CalculateLoss; // 跳到计算跌幅部分
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
                var startDate = DateTime.Today.AddDays(-config.NDays + 1);
                var endDate = DateTime.Today.AddDays(1);
                
                var nDayKlines = klines
                    .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date < endDate.Date)
                    .ToList();
                
                if (nDayKlines.Count == 0)
                    return null;
                
                // 4. 找到N天内的最高价
                highestPrice = nDayKlines.Max(k => k.HighPrice);
                if (highestPrice <= 0)
                    return null;
                
                // 5. 更新缓存
                await _cacheLock.WaitAsync();
                try
                {
                    _nDayHighPriceCache[ticker.Symbol] = (highestPrice, DateTime.Now, config.NDays);
                    _logger.LogDebug($"{ticker.Symbol} 计算并缓存{config.NDays}天最高价: {highestPrice}");
                }
                finally
                {
                    _cacheLock.Release();
                }
                
            CalculateLoss:
                
                // 4. 计算最新价相对于最高价的跌幅（负值）
                var lossPercent = ((ticker.LastPrice - highestPrice) / highestPrice) * 100m;
                
                // 5. 获取流通市值
                var contractInfo = _contractInfoService.GetContractInfo(ticker.Symbol);
                var circulatingMarketCap = 0m;
                if (contractInfo != null && contractInfo.CirculatingSupply > 0)
                {
                    circulatingMarketCap = contractInfo.CirculatingSupply * ticker.LastPrice;
                }
                
                return new LoserContract
                {
                    Symbol = ticker.Symbol,
                    LastPrice = ticker.LastPrice,
                    NDayHighPrice = highestPrice,
                    NDayLossPercent = lossPercent,
                    PriceChangePercent24h = ticker.PriceChangePercent,
                    QuoteVolume24h = ticker.QuoteVolume,
                    CirculatingMarketCap = circulatingMarketCap
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"计算 {ticker.Symbol} 的{config.NDays}天跌幅时出错: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 更新缓存区数据
        /// </summary>
        public async Task UpdateCachedContractsAsync(
            List<LoserContract> topLosers,
            Dictionary<string, CachedLoserContract> cachedContracts,
            LoserTrackingConfig config)
        {
            // 1. 添加新进榜的合约到缓存区
            foreach (var loser in topLosers)
            {
                if (cachedContracts.ContainsKey(loser.Symbol))
                {
                    // 已存在：重置倒计时，更新数据
                    var cached = cachedContracts[loser.Symbol];
                    cached.LastPrice = loser.LastPrice;
                    cached.NDayHighPrice = loser.NDayHighPrice;
                    cached.NDayLossPercent = loser.NDayLossPercent;
                    cached.PriceChangePercent24h = loser.PriceChangePercent24h;
                    cached.QuoteVolume24h = loser.QuoteVolume24h;
                    cached.CirculatingMarketCap = loser.CirculatingMarketCap;
                    cached.Rank = loser.Rank;
                    
                    // 重置倒计时
                    var now = DateTime.Now;
                    cached.CountdownStartTime = now;
                    cached.ExpiryTime = now.AddHours(config.CacheExpiryHours);
                    
                    // 更新录入后最低价
                    if (loser.LastPrice < cached.LowestPriceAfterEntry)
                    {
                        cached.LowestPriceAfterEntry = loser.LastPrice;
                    }
                    
                    _logger.LogDebug($"更新跌幅榜缓存: {loser.Symbol}, 排名{loser.Rank}, 重置倒计时");
                }
                else
                {
                    // 新合约：添加到缓存区
                    var now = DateTime.Now;
                    var cached = new CachedLoserContract
                    {
                        Symbol = loser.Symbol,
                        LastPrice = loser.LastPrice,
                        NDayHighPrice = loser.NDayHighPrice,
                        NDayLossPercent = loser.NDayLossPercent,
                        PriceChangePercent24h = loser.PriceChangePercent24h,
                        QuoteVolume24h = loser.QuoteVolume24h,
                        CirculatingMarketCap = loser.CirculatingMarketCap,
                        Rank = loser.Rank,
                        EntryTime = now,
                        EntryPrice = loser.LastPrice,
                        EntryRank = loser.Rank,
                        LowestPriceAfterEntry = loser.LastPrice,
                        CountdownStartTime = now,
                        ExpiryTime = now.AddHours(config.CacheExpiryHours)
                    };
                    
                    cachedContracts[loser.Symbol] = cached;
                    _logger.LogInformation($"新增跌幅榜合约到缓存: {loser.Symbol}, 排名{loser.Rank}");
                }
            }
            
            // 2. 批量更新不在榜单上的合约数据
            var currentSymbols = new HashSet<string>(topLosers.Select(l => l.Symbol));
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
                            
                            // 更新录入后最低价
                            if (ticker.LastPrice < cached.LowestPriceAfterEntry)
                            {
                                cached.LowestPriceAfterEntry = ticker.LastPrice;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "批量更新跌幅榜缓存合约价格失败");
                }
            }
        }
        
        /// <summary>
        /// 清理过期缓存，移动到回收区
        /// </summary>
        public void CleanExpiredCache(
            Dictionary<string, CachedLoserContract> cachedContracts,
            Dictionary<string, RecycledLoserContract> recycledContracts)
        {
            var expiredSymbols = cachedContracts
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var symbol in expiredSymbols)
            {
                var cached = cachedContracts[symbol];
                
                // 移动到回收区
                var recycled = new RecycledLoserContract
                {
                    Symbol = cached.Symbol,
                    LastPrice = cached.LastPrice,
                    NDayHighPrice = cached.NDayHighPrice,
                    NDayLossPercent = cached.NDayLossPercent,
                    PriceChangePercent24h = cached.PriceChangePercent24h,
                    QuoteVolume24h = cached.QuoteVolume24h,
                    CirculatingMarketCap = cached.CirculatingMarketCap,
                    Rank = cached.Rank,
                    RecycleTime = DateTime.Now,
                    CachedDurationHours = cached.CachedDurationHours
                };
                
                recycledContracts[symbol] = recycled;
                cachedContracts.Remove(symbol);
                
                _logger.LogInformation($"跌幅榜合约过期并移至回收区: {symbol}");
            }
        }
        
        /// <summary>
        /// 清理回收区（保留3天）
        /// </summary>
        public void CleanRecycledContracts(Dictionary<string, RecycledLoserContract> recycledContracts)
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
        public async Task SaveDataAsync(string instanceId, LoserTrackingData data)
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
                
                _logger.LogDebug($"跌幅榜数据已保存到: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存跌幅榜数据失败");
            }
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        public async Task<LoserTrackingData?> LoadDataAsync(string instanceId)
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, $"{instanceId}.json");
                
                if (!File.Exists(filePath))
                {
                    _logger.LogInformation($"跌幅榜数据文件不存在: {filePath}");
                    return null;
                }
                
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var data = JsonSerializer.Deserialize<LoserTrackingData>(json, options);
                
                if (data != null)
                {
                    _logger.LogInformation($"成功加载跌幅榜数据: 缓存={data.CachedContracts.Count}, 回收={data.RecycledContracts.Count}");
                }
                
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载跌幅榜数据失败");
                return null;
            }
        }
    }
}

