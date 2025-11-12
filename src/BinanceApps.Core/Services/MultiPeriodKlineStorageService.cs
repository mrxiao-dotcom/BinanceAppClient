using System.Text.Json;
using BinanceApps.Core.Models;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 多周期K线数据本地存储服务 - 支持1d、2h、1h、30m、15m、5m等多个周期
    /// </summary>
    public class MultiPeriodKlineStorageService
    {
        private readonly string _baseStorageDirectory;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly ILogger<MultiPeriodKlineStorageService>? _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        
        // 内存缓存：存储已加载的K线数据，避免重复获取
        // Key格式: "BTCUSDT_1d" (symbol_period)
        private readonly Dictionary<string, (List<Kline> Klines, DateTime CacheTime)> _memoryCache;
        private readonly object _cacheLock = new object();
        
        // 根据不同周期设置不同的缓存时长
        private TimeSpan GetCacheExpiration(string period)
        {
            return period switch
            {
                "1w" => TimeSpan.FromHours(24),   // 周线：24小时
                "1d" => TimeSpan.FromHours(2),    // 日线：2小时
                "2h" => TimeSpan.FromHours(1),    // 2小时线：1小时
                "1h" => TimeSpan.FromMinutes(30), // 1小时线：30分钟
                "30m" => TimeSpan.FromMinutes(15),// 30分钟线：15分钟
                "15m" => TimeSpan.FromMinutes(10),// 15分钟线：10分钟
                "5m" => TimeSpan.FromMinutes(5),  // 5分钟线：5分钟
                _ => TimeSpan.FromHours(1)        // 默认：1小时
            };
        }

        public MultiPeriodKlineStorageService(
            IBinanceSimulatedApiClient apiClient,
            ILogger<MultiPeriodKlineStorageService>? logger = null,
            string baseStorageDirectory = "KlineData")
        {
            _apiClient = apiClient;
            _logger = logger;
            _baseStorageDirectory = baseStorageDirectory;
            _memoryCache = new Dictionary<string, (List<Kline>, DateTime)>();
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // 确保基础存储目录存在
            if (!Directory.Exists(_baseStorageDirectory))
            {
                Directory.CreateDirectory(_baseStorageDirectory);
            }
            
            // 输出缓存策略信息
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔧 K线数据缓存策略:");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 1w (周线): 24小时");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 1d (日线): 2小时");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 2h: 1小时");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 1h: 30分钟");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 30m: 15分钟");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 15m: 10分钟");
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   • 5m: 5分钟");
        }

        /// <summary>
        /// 获取缓存键
        /// </summary>
        private string GetCacheKey(string symbol, string period)
        {
            return $"{symbol}_{period}";
        }

        /// <summary>
        /// 使用Ticker最新价更新今日K线的收盘价
        /// </summary>
        private async Task<List<Kline>> UpdateTodayKlineWithTickerAsync(List<Kline> klines, string symbol)
        {
            try
            {
                if (klines.Count == 0) return klines;

                var lastKline = klines.Last();
                var today = DateTime.UtcNow.Date;
                var lastKlineDate = lastKline.OpenTime.Date;

                // 只有最后一根K线是今天的，才需要更新
                if (lastKlineDate != today)
                {
                    return klines;
                }

                // 获取ticker最新价
                var ticker = await _apiClient.Get24hrPriceStatisticsAsync(symbol);
                if (ticker != null && ticker.LastPrice > 0)
                {
                    lastKline.ClosePrice = ticker.LastPrice;
                    lastKline.HighPrice = Math.Max(lastKline.HighPrice, ticker.LastPrice);
                    lastKline.LowPrice = Math.Min(lastKline.LowPrice, ticker.LastPrice);
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 {symbol} 使用Ticker更新今日K线: 收盘价 {ticker.LastPrice:F8}");
                }

                return klines;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"使用Ticker更新 {symbol} 今日K线失败");
                return klines; // 失败时返回原始数据
            }
        }

        /// <summary>
        /// 获取指定周期的存储目录
        /// </summary>
        private string GetPeriodDirectory(string period)
        {
            var periodDir = Path.Combine(_baseStorageDirectory, period);
            if (!Directory.Exists(periodDir))
            {
                Directory.CreateDirectory(periodDir);
            }
            return periodDir;
        }

        /// <summary>
        /// 获取K线数据文件路径
        /// </summary>
        private string GetKlineDataFilePath(string symbol, string period)
        {
            var periodDir = GetPeriodDirectory(period);
            return Path.Combine(periodDir, $"{symbol}.json");
        }

        /// <summary>
        /// 将周期字符串转换为KlineInterval枚举
        /// </summary>
        private KlineInterval PeriodToInterval(string period)
        {
            return period switch
            {
                "1w" => KlineInterval.OneWeek,
                "1d" => KlineInterval.OneDay,
                "2h" => KlineInterval.TwoHours,
                "1h" => KlineInterval.OneHour,
                "30m" => KlineInterval.ThirtyMinutes,
                "15m" => KlineInterval.FifteenMinutes,
                "5m" => KlineInterval.FiveMinutes,
                _ => KlineInterval.OneHour
            };
        }

        /// <summary>
        /// 计算周期的时间跨度（分钟）
        /// </summary>
        private int GetPeriodMinutes(string period)
        {
            return period switch
            {
                "1w" => 10080,  // 7 * 24 * 60
                "1d" => 1440,   // 24 * 60
                "2h" => 120,
                "1h" => 60,
                "30m" => 30,
                "15m" => 15,
                "5m" => 5,
                _ => 60
            };
        }

        /// <summary>
        /// 保存K线数据到本地文件
        /// </summary>
        private async Task<(bool Success, string? ErrorMessage)> SaveKlineDataAsync(
            string symbol, 
            string period, 
            List<Kline> klines)
        {
            try
            {
                var filePath = GetKlineDataFilePath(symbol, period);
                var klineData = new KlineDataFile
                {
                    Symbol = symbol,
                    LastUpdated = DateTime.UtcNow,
                    Klines = klines.OrderBy(k => k.OpenTime).ToList()
                };

                var json = JsonSerializer.Serialize(klineData, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"保存 {symbol} ({period}) K线数据失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 保存 {symbol} ({period}) K线数据失败: {ex.Message}");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 从本地文件加载K线数据
        /// </summary>
        private async Task<(List<Kline>? Klines, bool Success, string? ErrorMessage)> LoadKlineDataAsync(
            string symbol, 
            string period)
        {
            try
            {
                var filePath = GetKlineDataFilePath(symbol, period);
                if (!File.Exists(filePath))
                {
                    return (null, false, "文件不存在");
                }

                var json = await File.ReadAllTextAsync(filePath);
                var klineData = JsonSerializer.Deserialize<KlineDataFile>(json, _jsonOptions);
                
                if (klineData == null || klineData.Klines == null)
                {
                    return (null, false, "数据解析失败");
                }

                return (klineData.Klines, true, null);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"加载 {symbol} ({period}) K线数据失败");
                return (null, false, ex.Message);
            }
        }

        /// <summary>
        /// 合并K线数据（智能处理最后一条K线）
        /// </summary>
        private async Task<(List<Kline> MergedKlines, int NewCount, int UpdatedCount)> MergeKlineDataAsync(
            List<Kline> existingKlines,
            List<Kline> newKlines)
        {
            var result = new Dictionary<DateTime, Kline>();
            int newCount = 0;
            int updatedCount = 0;

            // 先添加所有现有K线
            foreach (var kline in existingKlines)
            {
                result[kline.OpenTime] = kline;
            }

            // 找到本地最后一条K线的时间
            var lastLocalTime = existingKlines.Count > 0 
                ? existingKlines.Max(k => k.OpenTime) 
                : DateTime.MinValue;

            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            // 合并新K线
            foreach (var newKline in newKlines)
            {
                var klineDate = newKline.OpenTime.Date;
                bool shouldUpdate = false;

                if (!result.ContainsKey(newKline.OpenTime))
                {
                    // 新K线
                    shouldUpdate = true;
                    newCount++;
                }
                else if (klineDate == today)
                {
                    // 今天的K线：始终更新
                    shouldUpdate = true;
                    updatedCount++;
                }
                else if (klineDate == yesterday)
                {
                    // 昨天的K线：始终更新
                    shouldUpdate = true;
                    updatedCount++;
                }
                else if (newKline.OpenTime == lastLocalTime)
                {
                    // 本地最后一条K线：始终更新（确保数据完整性）
                    shouldUpdate = true;
                    updatedCount++;
                }
                else if (IsDataDifferent(result[newKline.OpenTime], newKline))
                {
                    // 历史数据不同：更新
                    shouldUpdate = true;
                    updatedCount++;
                }

                if (shouldUpdate)
                {
                    result[newKline.OpenTime] = newKline;
                }
            }

            var mergedKlines = result.Values.OrderBy(k => k.OpenTime).ToList();
            return await Task.FromResult((mergedKlines, newCount, updatedCount));
        }

        /// <summary>
        /// 判断K线数据是否不同
        /// </summary>
        private bool IsDataDifferent(Kline existing, Kline newKline)
        {
            return existing.OpenPrice != newKline.OpenPrice ||
                   existing.HighPrice != newKline.HighPrice ||
                   existing.LowPrice != newKline.LowPrice ||
                   existing.ClosePrice != newKline.ClosePrice ||
                   existing.Volume != newKline.Volume;
        }

        /// <summary>
        /// 增量获取K线数据（核心方法）
        /// 1. 检查内存缓存
        /// 2. 从本地文件加载现有数据
        /// 3. 如果今日K线已存在，使用Ticker更新
        /// 4. 如果数据过旧，增量下载
        /// 5. 保存到本地文件和缓存
        /// </summary>
        public async Task<List<Kline>> GetKlineDataWithIncrementalUpdateAsync(
            string symbol,
            string period,
            int limit)
        {
            try
            {
                var cacheKey = GetCacheKey(symbol, period);
                
                // 步骤0: 检查内存缓存
                var cacheExpiration = GetCacheExpiration(period);
                lock (_cacheLock)
                {
                    if (_memoryCache.TryGetValue(cacheKey, out var cached))
                    {
                        var age = DateTime.Now - cached.CacheTime;
                        if (age < cacheExpiration)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💾💾💾 [{symbol}] {period} 使用内存缓存 (缓存时间: {age.TotalMinutes:F1}分钟前, 共{cached.Klines.Count}条, 有效期:{cacheExpiration.TotalMinutes:F0}分钟)");
                            return new List<Kline>(cached.Klines); // 返回副本
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏰ [{symbol}] {period} 缓存已过期 ({age.TotalMinutes:F1}分钟 > {cacheExpiration.TotalMinutes:F0}分钟)，重新加载");
                            _memoryCache.Remove(cacheKey);
                        }
                    }
                }

                var interval = PeriodToInterval(period);
                var periodMinutes = GetPeriodMinutes(period);

                // 步骤1: 加载本地数据
                var (existingKlines, loadSuccess, loadError) = await LoadKlineDataAsync(symbol, period);

                if (!loadSuccess || existingKlines == null || existingKlines.Count == 0)
                {
                    // 本地无数据，直接下载
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📥📥📥 [{symbol}] {period} 本地无数据，从API下载 {limit} 条");
                    var klines = await _apiClient.GetKlinesAsync(symbol, interval, limit);
                    
                    if (klines != null && klines.Count > 0)
                    {
                        await SaveKlineDataAsync(symbol, period, klines);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ [{symbol}] {period} API下载完成: {klines.Count} 条 → 已存入缓存");
                        
                        // 保存到缓存
                        lock (_cacheLock)
                        {
                            _memoryCache[cacheKey] = (klines, DateTime.Now);
                        }
                    }
                    
                    return klines ?? new List<Kline>();
                }

                // 步骤2: 检查本地数据是否需要更新
                var sortedExisting = existingKlines.OrderBy(k => k.OpenTime).ToList();
                var lastKlineTime = sortedExisting.Last().OpenTime;
                var now = DateTime.UtcNow;
                var timeDiff = now - lastKlineTime;
                var periodsNeeded = (int)Math.Ceiling(timeDiff.TotalMinutes / periodMinutes) + 2; // +2 确保覆盖最新数据

                if (periodsNeeded <= 1)
                {
                    // 数据足够新
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📂✅ [{symbol}] {period} 本地文件最新，共 {sortedExisting.Count} 条 (无需下载)");
                    
                    // 如果最后一根K线是今天的，使用Ticker更新（避免重复下载）
                    var lastKlineDate = sortedExisting.Last().OpenTime.Date;
                    var today = DateTime.UtcNow.Date;
                    if (lastKlineDate == today)
                    {
                        sortedExisting = await UpdateTodayKlineWithTickerAsync(sortedExisting, symbol);
                        await SaveKlineDataAsync(symbol, period, sortedExisting);
                    }
                    
                    // 如果本地数据不足limit，扩展到limit
                    if (sortedExisting.Count < limit)
                    {
                        var additionalNeeded = limit - sortedExisting.Count;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📥 {symbol} ({period}) 本地数据不足，补充下载 {additionalNeeded} 条");
                        
                        var allKlines = await _apiClient.GetKlinesAsync(symbol, interval, limit);
                        if (allKlines != null && allKlines.Count > 0)
                        {
                            var (mergedKlines, newCount, updatedCount) = await MergeKlineDataAsync(sortedExisting, allKlines);
                            await SaveKlineDataAsync(symbol, period, mergedKlines);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {symbol} ({period}) 数据补充完成: 新增 {newCount}, 更新 {updatedCount}");
                            
                            // 保存到缓存
                            lock (_cacheLock)
                            {
                                _memoryCache[cacheKey] = (mergedKlines, DateTime.Now);
                            }
                            
                            return mergedKlines;
                        }
                    }
                    
                    // 保存到缓存
                    lock (_cacheLock)
                    {
                        _memoryCache[cacheKey] = (sortedExisting, DateTime.Now);
                    }
                    
                    return sortedExisting;
                }

                // 步骤3: 增量下载新数据
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 {symbol} ({period}) 增量更新: 本地 {sortedExisting.Count} 条，需下载 {periodsNeeded} 条");
                
                var newKlines = await _apiClient.GetKlinesAsync(symbol, interval, periodsNeeded);
                
                if (newKlines == null || newKlines.Count == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ {symbol} ({period}) 下载失败，使用本地数据");
                    return sortedExisting;
                }

                // 步骤4: 合并数据
                var (merged, newCnt, updatedCnt) = await MergeKlineDataAsync(sortedExisting, newKlines);
                
                // 步骤5: 保存到本地
                await SaveKlineDataAsync(symbol, period, merged);
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {symbol} ({period}) 增量更新完成: 总 {merged.Count} 条 (新增 {newCnt}, 更新 {updatedCnt})");

                // 如果合并后的数据仍然不足limit，再下载完整的limit条
                if (merged.Count < limit)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📥 {symbol} ({period}) 合并后数据不足，下载完整 {limit} 条");
                    var fullKlines = await _apiClient.GetKlinesAsync(symbol, interval, limit);
                    
                    if (fullKlines != null && fullKlines.Count > 0)
                    {
                        var (finalMerged, finalNew, finalUpdated) = await MergeKlineDataAsync(merged, fullKlines);
                        await SaveKlineDataAsync(symbol, period, finalMerged);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {symbol} ({period}) 完整数据下载完成: 总 {finalMerged.Count} 条");
                        
                        // 保存到缓存
                        lock (_cacheLock)
                        {
                            _memoryCache[cacheKey] = (finalMerged, DateTime.Now);
                        }
                        
                        return finalMerged;
                    }
                }

                // 保存到缓存
                lock (_cacheLock)
                {
                    _memoryCache[cacheKey] = (merged, DateTime.Now);
                }

                return merged;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"增量获取 {symbol} ({period}) K线数据失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 增量获取 {symbol} ({period}) 失败: {ex.Message}");
                
                // 失败时尝试直接从API获取
                try
                {
                    var interval = PeriodToInterval(period);
                    var klines = await _apiClient.GetKlinesAsync(symbol, interval, limit);
                    return klines ?? new List<Kline>();
                }
                catch
                {
                    return new List<Kline>();
                }
            }
        }

        /// <summary>
        /// 批量增量获取多个合约的K线数据
        /// </summary>
        public async Task<Dictionary<string, List<Kline>>> BatchGetKlineDataAsync(
            List<string> symbols,
            string period,
            int limit,
            int maxConcurrency = 10)
        {
            var result = new Dictionary<string, List<Kline>>();
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            foreach (var symbol in symbols)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        var klines = await GetKlineDataWithIncrementalUpdateAsync(symbol, period, limit);
                        lock (result)
                        {
                            result[symbol] = klines;
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return result;
        }

        /// <summary>
        /// 清理指定周期的所有本地数据
        /// </summary>
        public void ClearPeriodData(string period)
        {
            try
            {
                var periodDir = GetPeriodDirectory(period);
                if (Directory.Exists(periodDir))
                {
                    Directory.Delete(periodDir, true);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑️ 已清理 {period} 周期的本地数据");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"清理 {period} 周期数据失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 清理 {period} 周期数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理内存缓存
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                var count = _memoryCache.Count;
                _memoryCache.Clear();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑️ 已清理内存缓存，共 {count} 条记录");
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int Count, long TotalKlines) GetCacheStats()
        {
            lock (_cacheLock)
            {
                var totalKlines = _memoryCache.Values.Sum(v => v.Klines.Count);
                return (_memoryCache.Count, totalKlines);
            }
        }
    }
}

