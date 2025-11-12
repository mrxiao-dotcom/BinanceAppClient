using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 小时均线监控服务
    /// </summary>
    public class HourlyEmaService : IHourlyEmaService
    {
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly ILogger<HourlyEmaService>? _logger;
        private readonly string _storageDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        // 缓存数据：合约名 -> K线和EMA数据
        private Dictionary<string, HourlyKlineData> _cachedData = new Dictionary<string, HourlyKlineData>();
        private readonly object _cacheLock = new object();

        public HourlyEmaService(
            IBinanceSimulatedApiClient apiClient,
            KlineDataStorageService klineStorageService,
            ILogger<HourlyEmaService>? logger = null)
        {
            _apiClient = apiClient;
            _klineStorageService = klineStorageService;
            _logger = logger;
            _storageDirectory = Path.Combine("KlineData", "HourlyEma");
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // 确保存储目录存在
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        /// <summary>
        /// 获取所有可交易合约的小时K线数据
        /// </summary>
        public async Task<bool> FetchHourlyKlinesAsync(HourlyEmaParameters parameters, Action<HourlyKlineDownloadProgress>? progressCallback = null)
        {
            try
            {
                _logger?.LogInformation("开始获取小时K线数据...");
                Console.WriteLine("📊 开始获取小时K线数据...");

                // 获取所有可交易的合约
                var symbolsInfo = await _apiClient.GetAllSymbolsInfoAsync();
                if (symbolsInfo == null || symbolsInfo.Count == 0)
                {
                    _logger?.LogWarning("未找到可交易的合约");
                    Console.WriteLine("⚠️ 未找到可交易的合约");
                    return false;
                }

                var totalCount = symbolsInfo.Count;
                var completedCount = 0;

                Console.WriteLine($"📋 找到 {totalCount} 个可交易合约");

                // 清空缓存
                lock (_cacheLock)
                {
                    _cachedData.Clear();
                }

                // 第一步：批量并行加载本地K线数据（性能优化）
                Console.WriteLine("📦 第1步：批量加载本地K线数据...");
                var symbols = symbolsInfo.Select(s => s.Symbol).ToList();
                var localKlines = await _klineStorageService.LoadKlineDataBatchAsync(
                    symbols, 
                    maxDegreeOfParallelism: 30,
                    progressCallback: (completed, total) =>
                    {
                        progressCallback?.Invoke(new HourlyKlineDownloadProgress
                        {
                            TotalCount = totalCount,
                            CompletedCount = completed,
                            CurrentSymbol = $"加载本地数据 {completed}/{total}"
                        });
                    });

                Console.WriteLine($"📊 本地加载完成: {localKlines.Count}/{totalCount} 个合约");

                // 第二步：筛选出需要从API下载的合约
                var symbolsNeedDownload = new List<string>();
                var symbolsUseLocal = new Dictionary<string, List<Kline>>();

                foreach (var symbol in symbols)
                {
                    if (localKlines.TryGetValue(symbol, out var existingKlines))
                    {
                        // 检查数据是否足够且为1小时周期
                        bool isValid = existingKlines.Count >= parameters.KlineCount;
                        
                        if (isValid && existingKlines.Count >= 2)
                        {
                            var sortedKlines = existingKlines.OrderBy(k => k.OpenTime).ToList();
                            var timeDiff = sortedKlines[1].OpenTime - sortedKlines[0].OpenTime;
                            isValid = Math.Abs(timeDiff.TotalHours - 1.0) < 0.1;
                        }

                        if (isValid)
                        {
                            // 取最近X根
                            var recentKlines = existingKlines
                                .OrderByDescending(k => k.OpenTime)
                                .Take(parameters.KlineCount)
                                .OrderBy(k => k.OpenTime)
                                .ToList();
                            symbolsUseLocal[symbol] = recentKlines;
                        }
                        else
                        {
                            symbolsNeedDownload.Add(symbol);
                        }
                    }
                    else
                    {
                        symbolsNeedDownload.Add(symbol);
                    }
                }

                Console.WriteLine($"✅ 使用本地数据: {symbolsUseLocal.Count} 个合约");
                Console.WriteLine($"🔄 需要下载: {symbolsNeedDownload.Count} 个合约");

                // 第三步：从本地数据创建缓存
                foreach (var kvp in symbolsUseLocal)
                {
                    var klineData = new HourlyKlineData
                    {
                        Symbol = kvp.Key,
                        Klines = kvp.Value,
                        LastUpdateTime = DateTime.Now
                    };

                    lock (_cacheLock)
                    {
                        _cachedData[kvp.Key] = klineData;
                    }
                }

                // 第四步：并行下载缺失的数据（使用信号量控制并发）
                if (symbolsNeedDownload.Count > 0)
                {
                    Console.WriteLine($"📥 第2步：并行下载缺失的K线数据...");
                    var downloadSemaphore = new SemaphoreSlim(10); // 控制并发数为10
                    var downloadCompletedCount = 0;
                    
                    var downloadTasks = symbolsNeedDownload.Select(async symbol =>
                    {
                        await downloadSemaphore.WaitAsync();
                        try
                        {
                            // 从API获取K线
                            var klines = await _apiClient.GetKlinesAsync(symbol, KlineInterval.OneHour, parameters.KlineCount);
                            
                            if (klines != null && klines.Count > 0)
                            {
                                // 保存到本地
                                await _klineStorageService.SaveKlineDataAsync(symbol, klines);
                                
                                // 添加到缓存
                                var sortedKlines = klines.OrderBy(k => k.OpenTime).ToList();
                                var klineData = new HourlyKlineData
                                {
                                    Symbol = symbol,
                                    Klines = sortedKlines,
                                    LastUpdateTime = DateTime.Now
                                };

                                lock (_cacheLock)
                                {
                                    _cachedData[symbol] = klineData;
                                }
                            }
                            
                            var completed = Interlocked.Increment(ref downloadCompletedCount);
                            
                            // 每10个或每10%报告一次进度
                            if (completed % 10 == 0 || completed % (symbolsNeedDownload.Count / 10 + 1) == 0)
                            {
                                Console.WriteLine($"📥 下载进度: {completed}/{symbolsNeedDownload.Count} ({completed * 100 / symbolsNeedDownload.Count}%)");
                                
                                progressCallback?.Invoke(new HourlyKlineDownloadProgress
                                {
                                    TotalCount = totalCount,
                                    CompletedCount = symbolsUseLocal.Count + completed,
                                    CurrentSymbol = $"下载中 {completed}/{symbolsNeedDownload.Count}"
                                });
                            }
                            
                            // 减少延迟到50ms
                            await Task.Delay(50);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, $"下载 {symbol} 失败");
                            Interlocked.Increment(ref downloadCompletedCount);
                        }
                        finally
                        {
                            downloadSemaphore.Release();
                        }
                    }).ToArray();

                    await Task.WhenAll(downloadTasks);
                    Console.WriteLine($"✅ 下载完成: {downloadCompletedCount}/{symbolsNeedDownload.Count}");
                }

                // 更新最终进度
                progressCallback?.Invoke(new HourlyKlineDownloadProgress
                {
                    TotalCount = totalCount,
                    CompletedCount = completedCount,
                    CurrentSymbol = string.Empty
                });

                Console.WriteLine($"✅ 小时K线数据获取完成！成功: {_cachedData.Count}/{totalCount}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取小时K线数据失败");
                Console.WriteLine($"❌ 获取小时K线数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算EMA均线数据
        /// </summary>
        public async Task<bool> CalculateEmaAsync(HourlyEmaParameters parameters)
        {
            try
            {
                _logger?.LogInformation("开始计算EMA均线...");
                Console.WriteLine("📊 开始计算EMA均线...");

                Dictionary<string, HourlyKlineData> dataToProcess;
                lock (_cacheLock)
                {
                    dataToProcess = new Dictionary<string, HourlyKlineData>(_cachedData);
                }

                if (dataToProcess.Count == 0)
                {
                    _logger?.LogWarning("没有可用的K线数据，请先获取K线数据");
                    Console.WriteLine("⚠️ 没有可用的K线数据，请先获取K线数据");
                    return false;
                }

                var successCount = 0;
                foreach (var kvp in dataToProcess)
                {
                    var symbol = kvp.Key;
                    var klineData = kvp.Value;

                    try
                    {
                        if (klineData.Klines.Count < parameters.EmaPeriod)
                        {
                            Console.WriteLine($"⚠️ {symbol} K线数据不足（需要{parameters.EmaPeriod}根，实际{klineData.Klines.Count}根），跳过");
                            continue;
                        }

                        // 按时间排序
                        var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();

                        // 计算EMA
                        var emaValues = CalculateEMA(sortedKlines, parameters.EmaPeriod);
                        
                        // 更新缓存中的EMA数据
                        lock (_cacheLock)
                        {
                            if (_cachedData.ContainsKey(symbol))
                            {
                                _cachedData[symbol].EmaValues = emaValues;
                            }
                        }

                        // 保存到文件
                        await SaveKlineDataToFileAsync(symbol, klineData);

                        successCount++;
                        Console.WriteLine($"✅ {symbol} EMA计算完成：{emaValues.Count} 个数据点");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"计算 {symbol} 的EMA失败");
                        Console.WriteLine($"❌ 计算 {symbol} 的EMA失败: {ex.Message}");
                    }
                }

                Console.WriteLine($"✅ EMA计算完成！成功: {successCount}/{dataToProcess.Count}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "计算EMA均线失败");
                Console.WriteLine($"❌ 计算EMA均线失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 增量更新K线数据（从最后一个K线到现在）
        /// </summary>
        public async Task<bool> UpdateHourlyKlinesAsync(Action<HourlyKlineDownloadProgress>? progressCallback = null)
        {
            try
            {
                _logger?.LogInformation("开始增量更新K线数据...");
                Console.WriteLine("🔄 开始增量更新K线数据...");

                Dictionary<string, HourlyKlineData> dataSnapshot;
                lock (_cacheLock)
                {
                    dataSnapshot = new Dictionary<string, HourlyKlineData>(_cachedData);
                }

                if (dataSnapshot.Count == 0)
                {
                    _logger?.LogWarning("没有可用的缓存数据，请先获取K线数据");
                    Console.WriteLine("⚠️ 没有可用的缓存数据，请先获取K线数据");
                    return false;
                }

                var totalCount = dataSnapshot.Count;
                var now = DateTime.UtcNow;
                
                // 第一步：筛选出需要更新的合约
                var symbolsNeedUpdate = new List<(string Symbol, int KlinesNeeded)>();
                
                foreach (var kvp in dataSnapshot)
                {
                    var symbol = kvp.Key;
                    var klineData = kvp.Value;
                    
                    var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();
                    if (sortedKlines.Count == 0) continue;

                    var lastKlineTime = sortedKlines.Last().OpenTime;
                    var hoursSinceLastKline = (now - lastKlineTime).TotalHours;

                    if (hoursSinceLastKline >= 1.0)
                    {
                        var klinesNeeded = (int)Math.Ceiling(hoursSinceLastKline) + 1;
                        symbolsNeedUpdate.Add((symbol, klinesNeeded));
                    }
                }

                Console.WriteLine($"📊 总合约数: {totalCount}, 需要更新: {symbolsNeedUpdate.Count}");
                
                if (symbolsNeedUpdate.Count == 0)
                {
                    Console.WriteLine($"✅ 所有K线数据都是最新的");
                    return true;
                }

                // 第二步：并行更新（使用信号量控制并发）
                var updateSemaphore = new SemaphoreSlim(10);
                var updateCompletedCount = 0;
                var updateSuccessCount = 0;
                
                var updateTasks = symbolsNeedUpdate.Select(async item =>
                {
                    await updateSemaphore.WaitAsync();
                    try
                    {
                        var symbol = item.Symbol;
                        var klinesNeeded = item.KlinesNeeded;
                        
                        // 从API获取最新的K线
                        var newKlines = await _apiClient.GetKlinesAsync(symbol, KlineInterval.OneHour, klinesNeeded);
                        
                        if (newKlines != null && newKlines.Count > 0)
                        {
                            // 获取原有数据
                            HourlyKlineData? originalData = null;
                            lock (_cacheLock)
                            {
                                dataSnapshot.TryGetValue(symbol, out originalData);
                            }
                            
                            if (originalData != null)
                            {
                                var sortedKlines = originalData.Klines.OrderBy(k => k.OpenTime).ToList();
                                
                                // 移除最后一根K线（可能不完整）
                                if (sortedKlines.Count > 0)
                                {
                                    sortedKlines.RemoveAt(sortedKlines.Count - 1);
                                }
                                
                                // 添加新获取的K线
                                sortedKlines.AddRange(newKlines);
                                
                                // 去重并排序
                                var uniqueKlines = sortedKlines
                                    .GroupBy(k => k.OpenTime)
                                    .Select(g => g.First())
                                    .OrderBy(k => k.OpenTime)
                                    .ToList();

                                // 更新缓存
                                lock (_cacheLock)
                                {
                                    if (_cachedData.ContainsKey(symbol))
                                    {
                                        _cachedData[symbol].Klines = uniqueKlines;
                                        _cachedData[symbol].LastUpdateTime = DateTime.Now;
                                    }
                                }

                                // 保存到本地
                                await _klineStorageService.SaveKlineDataAsync(symbol, uniqueKlines);
                                Interlocked.Increment(ref updateSuccessCount);
                            }
                        }
                        
                        var completed = Interlocked.Increment(ref updateCompletedCount);
                        
                        // 每10个或每10%报告一次进度
                        if (completed % 10 == 0 || completed % (symbolsNeedUpdate.Count / 10 + 1) == 0)
                        {
                            Console.WriteLine($"🔄 更新进度: {completed}/{symbolsNeedUpdate.Count} ({completed * 100 / symbolsNeedUpdate.Count}%)");
                            
                            progressCallback?.Invoke(new HourlyKlineDownloadProgress
                            {
                                TotalCount = totalCount,
                                CompletedCount = completed,
                                CurrentSymbol = $"更新中 {completed}/{symbolsNeedUpdate.Count}"
                            });
                        }
                        
                        // 减少延迟到30ms
                        await Task.Delay(30);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"更新 {item.Symbol} 失败");
                        Interlocked.Increment(ref updateCompletedCount);
                    }
                    finally
                    {
                        updateSemaphore.Release();
                    }
                }).ToArray();

                await Task.WhenAll(updateTasks);
                
                // 更新最终进度
                progressCallback?.Invoke(new HourlyKlineDownloadProgress
                {
                    TotalCount = totalCount,
                    CompletedCount = symbolsNeedUpdate.Count,
                    CurrentSymbol = string.Empty
                });

                Console.WriteLine($"✅ K线增量更新完成！更新了 {updateSuccessCount}/{symbolsNeedUpdate.Count} 个合约");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "增量更新K线数据失败");
                Console.WriteLine($"❌ 增量更新K线数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算连续大于/小于EMA的K线数量
        /// </summary>
        public Task<bool> CalculateAboveBelowEmaCountsAsync()
        {
            try
            {
                _logger?.LogInformation("开始计算连续大于/小于EMA的K线数量...");
                Console.WriteLine("📊 开始计算连续大于/小于EMA的K线数量...");

                Dictionary<string, HourlyKlineData> dataSnapshot;
                lock (_cacheLock)
                {
                    dataSnapshot = new Dictionary<string, HourlyKlineData>(_cachedData);
                }

                if (dataSnapshot.Count == 0)
                {
                    _logger?.LogWarning("没有可用的缓存数据");
                    Console.WriteLine("⚠️ 没有可用的缓存数据");
                    return Task.FromResult(false);
                }

                foreach (var kvp in dataSnapshot)
                {
                    var symbol = kvp.Key;
                    var klineData = kvp.Value;

                    try
                    {
                        if (klineData.EmaValues.Count == 0 || klineData.Klines.Count == 0)
                        {
                            continue;
                        }

                        // 按时间排序K线（从旧到新）
                        var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();
                        
                        // 计算连续大于/小于EMA的数量
                        var (aboveCount, belowCount) = CalculateAboveBelowEmaCount(sortedKlines, klineData.EmaValues);
                        
                        // 更新缓存中的数据
                        lock (_cacheLock)
                        {
                            if (_cachedData.ContainsKey(symbol))
                            {
                                // 将计数存储在专用字段中
                                _cachedData[symbol].AboveEmaCount = aboveCount;
                                _cachedData[symbol].BelowEmaCount = belowCount;
                            }
                        }

                        Console.WriteLine($"✅ {symbol}: 连续大于EMA={aboveCount}, 连续小于EMA={belowCount}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"计算 {symbol} 的连续数量失败");
                        Console.WriteLine($"❌ 计算 {symbol} 失败: {ex.Message}");
                    }
                }

                Console.WriteLine($"✅ 连续数量计算完成！");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "计算连续数量失败");
                Console.WriteLine($"❌ 计算连续数量失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 获取所有合约的监控结果
        /// </summary>
        public async Task<List<HourlyEmaMonitorResult>> GetMonitorResultsAsync(HourlyEmaFilter? filter = null)
        {
            var results = new List<HourlyEmaMonitorResult>();

            try
            {
                // 获取最新的ticker数据
                var tickers = await _apiClient.GetAllTicksAsync();
                if (tickers == null || tickers.Count == 0)
                {
                    return results;
                }

                Dictionary<string, HourlyKlineData> dataSnapshot;
                lock (_cacheLock)
                {
                    dataSnapshot = new Dictionary<string, HourlyKlineData>(_cachedData);
                }

                foreach (var kvp in dataSnapshot)
                {
                    var symbol = kvp.Key;
                    var klineData = kvp.Value;

                    try
                    {
                        // 找到对应的ticker
                        var ticker = tickers.FirstOrDefault(t => t.Symbol == symbol);
                        if (ticker == null)
                        {
                            continue;
                        }

                        // 获取最新的EMA值
                        if (klineData.EmaValues.Count > 0 && klineData.Klines.Count > 0)
                        {
                            var latestEma = klineData.EmaValues.Values.Last();
                            
                            // 使用最后K线的收盘价（与连续数量计算保持一致）
                            var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();
                            var lastKlineClose = sortedKlines.Last().ClosePrice;
                            
                            // 计算距离EMA的百分比
                            var distancePercent = latestEma != 0 ? ((lastKlineClose - latestEma) / latestEma * 100) : 0;

                            var result = new HourlyEmaMonitorResult
                            {
                                Symbol = symbol,
                                LastPrice = lastKlineClose,  // 使用K线收盘价而不是ticker实时价格
                                CurrentEma = latestEma,
                                DistancePercent = distancePercent,
                                PriceChangePercent = ticker.PriceChangePercent,
                                KlineCount = klineData.Klines.Count,
                                AboveEmaCount = klineData.AboveEmaCount,
                                BelowEmaCount = klineData.BelowEmaCount,
                                UpdateTime = DateTime.Now
                            };

                            // 应用筛选
                            if (filter != null)
                            {
                                if (filter.MinAboveEmaCount.HasValue && result.AboveEmaCount < filter.MinAboveEmaCount.Value)
                                {
                                    continue;
                                }
                                if (filter.MinBelowEmaCount.HasValue && result.BelowEmaCount < filter.MinBelowEmaCount.Value)
                                {
                                    continue;
                                }
                            }

                            results.Add(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"生成 {symbol} 的监控结果失败");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取监控结果失败");
                Console.WriteLine($"❌ 获取监控结果失败: {ex.Message}");
            }

            return results.OrderBy(r => r.Symbol).ToList();
        }

        /// <summary>
        /// 获取指定合约的K线和EMA数据
        /// </summary>
        public Task<HourlyKlineData?> GetHourlyKlineDataAsync(string symbol)
        {
            lock (_cacheLock)
            {
                if (_cachedData.TryGetValue(symbol, out var data))
                {
                    return Task.FromResult<HourlyKlineData?>(data);
                }
            }
            return Task.FromResult<HourlyKlineData?>(null);
        }

        /// <summary>
        /// 更新指定合约的最新价格并重新计算EMA（用于浮动监控窗口）
        /// </summary>
        public Task<bool> UpdateSymbolLatestPriceAndEmaAsync(string symbol, decimal latestPrice, int emaPeriod = 26)
        {
            try
            {
                HourlyKlineData? klineData = null;
                
                lock (_cacheLock)
                {
                    if (!_cachedData.TryGetValue(symbol, out klineData))
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ {symbol} 没有缓存的K线数据");
                        return Task.FromResult(false);
                    }
                }

                if (klineData.Klines.Count == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ {symbol} 的K线数据为空");
                    return Task.FromResult(false);
                }

                // 更新最后一根K线的收盘价
                var lastKline = klineData.Klines.Last();
                lastKline.ClosePrice = latestPrice;
                lastKline.HighPrice = Math.Max(lastKline.HighPrice, latestPrice);
                lastKline.LowPrice = Math.Min(lastKline.LowPrice, latestPrice);

                // 重新计算EMA
                var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();
                var emaValues = CalculateEMA(sortedKlines, emaPeriod);
                klineData.EmaValues = emaValues;

                // 重新计算连续大于/小于EMA的数量
                var (aboveCount, belowCount) = CalculateAboveBelowEmaCount(sortedKlines, emaValues);
                klineData.AboveEmaCount = aboveCount;
                klineData.BelowEmaCount = belowCount;

                klineData.LastUpdateTime = DateTime.Now;

                // 更新缓存
                lock (_cacheLock)
                {
                    _cachedData[symbol] = klineData;
                }

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"更新 {symbol} 的最新价格和EMA失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 更新 {symbol} 失败: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// 清除所有缓存数据
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedData.Clear();
            }
            Console.WriteLine("🗑️ 已清除所有缓存数据");
        }

        /// <summary>
        /// 获取缓存中的合约数量
        /// </summary>
        public int GetCachedSymbolCount()
        {
            lock (_cacheLock)
            {
                return _cachedData.Count;
            }
        }

        /// <summary>
        /// 获取最后一个K线的时间距离现在的小时数
        /// </summary>
        public double GetHoursSinceLastKline()
        {
            lock (_cacheLock)
            {
                if (_cachedData.Count == 0)
                    return double.MaxValue;

                var maxHours = 0.0;
                foreach (var kvp in _cachedData)
                {
                    if (kvp.Value.Klines.Count > 0)
                    {
                        var lastKline = kvp.Value.Klines.OrderBy(k => k.OpenTime).Last();
                        var hours = (DateTime.UtcNow - lastKline.OpenTime).TotalHours;
                        if (hours > maxHours)
                            maxHours = hours;
                    }
                }
                return maxHours;
            }
        }

        /// <summary>
        /// 检查K线是否在最近1小时内，不是则增量更新
        /// </summary>
        public async Task<bool> CheckAndUpdateKlinesIfNeededAsync()
        {
            try
            {
                var hoursSinceLastKline = GetHoursSinceLastKline();
                Console.WriteLine($"🔍 检查K线时间：距离现在 {hoursSinceLastKline:F1} 小时");

                if (hoursSinceLastKline >= 1.0)
                {
                    Console.WriteLine($"⚠️ K线数据超过1小时，开始增量更新...");
                    return await UpdateHourlyKlinesAsync();
                }
                else
                {
                    Console.WriteLine($"✅ K线数据在1小时内，无需更新");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "检查和更新K线失败");
                Console.WriteLine($"❌ 检查和更新K线失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 用Ticker价格更新所有合约最后一个K线的收盘价（仅缓存）
        /// </summary>
        public async Task<bool> UpdateLastKlineWithTickerAsync()
        {
            try
            {
                _logger?.LogInformation("开始用Ticker更新最后K线收盘价...");
                Console.WriteLine("📊 开始用Ticker更新最后K线收盘价...");

                // 获取所有ticker数据
                var tickers = await _apiClient.GetAllTicksAsync();
                if (tickers == null || tickers.Count == 0)
                {
                    Console.WriteLine("⚠️ 获取Ticker数据失败");
                    return false;
                }

                Dictionary<string, HourlyKlineData> dataSnapshot;
                lock (_cacheLock)
                {
                    dataSnapshot = new Dictionary<string, HourlyKlineData>(_cachedData);
                }

                var updateCount = 0;
                var now = DateTime.UtcNow;
                var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);

                foreach (var kvp in dataSnapshot)
                {
                    var symbol = kvp.Key;
                    var klineData = kvp.Value;

                    try
                    {
                        // 找到对应的ticker
                        var ticker = tickers.FirstOrDefault(t => t.Symbol == symbol);
                        if (ticker == null)
                        {
                            continue;
                        }

                        if (klineData.Klines.Count == 0)
                        {
                            continue;
                        }

                        // 按时间排序K线
                        var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();
                        var lastKline = sortedKlines.Last();

                        // 如果最后K线是当前整点，更新收盘价
                        if (lastKline.OpenTime == currentHour)
                        {
                            lastKline.ClosePrice = ticker.LastPrice;
                            lastKline.HighPrice = Math.Max(lastKline.HighPrice, ticker.LastPrice);
                            lastKline.LowPrice = Math.Min(lastKline.LowPrice == 0 ? ticker.LastPrice : lastKline.LowPrice, ticker.LastPrice);
                            
                            // 更新缓存
                            lock (_cacheLock)
                            {
                                if (_cachedData.ContainsKey(symbol))
                                {
                                    _cachedData[symbol].Klines = sortedKlines;
                                    _cachedData[symbol].LastUpdateTime = DateTime.Now;
                                }
                            }
                            updateCount++;
                        }
                        else
                        {
                            // 如果最后K线不是当前整点，需要添加新K线
                            var newKline = new Kline
                            {
                                Symbol = symbol,
                                OpenTime = currentHour,
                                OpenPrice = ticker.LastPrice,
                                HighPrice = ticker.LastPrice,
                                LowPrice = ticker.LastPrice,
                                ClosePrice = ticker.LastPrice,
                                Volume = 0,
                                QuoteVolume = 0
                            };

                            sortedKlines.Add(newKline);

                            // 更新缓存
                            lock (_cacheLock)
                            {
                                if (_cachedData.ContainsKey(symbol))
                                {
                                    _cachedData[symbol].Klines = sortedKlines;
                                    _cachedData[symbol].LastUpdateTime = DateTime.Now;
                                }
                            }
                            updateCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"更新 {symbol} 的最后K线失败");
                        Console.WriteLine($"❌ 更新 {symbol} 失败: {ex.Message}");
                    }
                }

                Console.WriteLine($"✅ Ticker更新完成！更新了 {updateCount}/{dataSnapshot.Count} 个合约");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "用Ticker更新最后K线失败");
                Console.WriteLine($"❌ 用Ticker更新最后K线失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算EMA（指数移动平均）
        /// </summary>
        private Dictionary<DateTime, decimal> CalculateEMA(List<Kline> klines, int period)
        {
            var emaValues = new Dictionary<DateTime, decimal>();
            
            if (klines.Count < period)
            {
                return emaValues;
            }

            // EMA计算公式：EMA(t) = Price(t) * k + EMA(t-1) * (1 - k)
            // 其中 k = 2 / (period + 1)
            decimal multiplier = 2.0m / (period + 1);

            // 第一个EMA值使用简单移动平均（SMA）
            decimal sma = klines.Take(period).Average(k => k.ClosePrice);
            emaValues[klines[period - 1].OpenTime] = sma;

            // 计算后续的EMA值
            for (int i = period; i < klines.Count; i++)
            {
                var currentPrice = klines[i].ClosePrice;
                var previousEma = emaValues[klines[i - 1].OpenTime];
                var currentEma = (currentPrice * multiplier) + (previousEma * (1 - multiplier));
                emaValues[klines[i].OpenTime] = currentEma;
            }

            return emaValues;
        }

        /// <summary>
        /// 计算连续大于/小于EMA的K线数量
        /// 从最新K线的close开始，向前计数直到遇到第一个反向的K线
        /// </summary>
        private (int AboveCount, int BelowCount) CalculateAboveBelowEmaCount(List<Kline> sortedKlines, Dictionary<DateTime, decimal> emaValues)
        {
            if (sortedKlines.Count == 0 || emaValues.Count == 0)
            {
                return (0, 0);
            }

            // 从最新（最后一根）K线开始向前查找
            int index = sortedKlines.Count - 1;
            
            // 找到最新K线对应的EMA值
            var latestKline = sortedKlines[index];
            if (!emaValues.ContainsKey(latestKline.OpenTime))
            {
                // 如果没有对应的EMA，向前查找最近的EMA
                var sortedEmaKeys = emaValues.Keys.Where(k => k <= latestKline.OpenTime).OrderByDescending(k => k).ToList();
                if (sortedEmaKeys.Count == 0)
                {
                    return (0, 0);
                }
                var latestEmaTime = sortedEmaKeys.First();
                // 找到对应的K线索引
                index = sortedKlines.FindLastIndex(k => k.OpenTime <= latestEmaTime);
                if (index < 0)
                {
                    return (0, 0);
                }
                latestKline = sortedKlines[index];
            }

            var latestEma = emaValues[latestKline.OpenTime];
            var latestClose = latestKline.ClosePrice;

            // 判断最新K线是大于还是小于EMA
            if (latestClose > latestEma)
            {
                // 最新K线大于EMA，计数连续大于EMA的K线
                int aboveCount = 0;
                for (int i = index; i >= 0; i--)
                {
                    var kline = sortedKlines[i];
                    if (!emaValues.ContainsKey(kline.OpenTime))
                    {
                        break; // 没有对应的EMA值，停止计数
                    }
                    
                    var ema = emaValues[kline.OpenTime];
                    if (kline.ClosePrice > ema)
                    {
                        aboveCount++;
                    }
                    else
                    {
                        break; // 遇到小于或等于EMA的K线，停止计数
                    }
                }
                return (aboveCount, 0);
            }
            else if (latestClose < latestEma)
            {
                // 最新K线小于EMA，计数连续小于EMA的K线
                int belowCount = 0;
                for (int i = index; i >= 0; i--)
                {
                    var kline = sortedKlines[i];
                    if (!emaValues.ContainsKey(kline.OpenTime))
                    {
                        break; // 没有对应的EMA值，停止计数
                    }
                    
                    var ema = emaValues[kline.OpenTime];
                    if (kline.ClosePrice < ema)
                    {
                        belowCount++;
                    }
                    else
                    {
                        break; // 遇到大于或等于EMA的K线，停止计数
                    }
                }
                return (0, belowCount);
            }
            else
            {
                // 正好等于EMA，返回0
                return (0, 0);
            }
        }

        /// <summary>
        /// 保存K线数据到文件
        /// </summary>
        private async Task SaveKlineDataToFileAsync(string symbol, HourlyKlineData klineData)
        {
            try
            {
                var filePath = Path.Combine(_storageDirectory, $"{symbol}_hourly.json");
                var json = JsonSerializer.Serialize(klineData, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"保存 {symbol} 的K线数据到文件失败");
                Console.WriteLine($"❌ 保存 {symbol} 的K线数据到文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载K线数据
        /// </summary>
        private async Task<HourlyKlineData?> LoadKlineDataFromFileAsync(string symbol)
        {
            try
            {
                var filePath = Path.Combine(_storageDirectory, $"{symbol}_hourly.json");
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<HourlyKlineData>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"从文件加载 {symbol} 的K线数据失败");
                Console.WriteLine($"❌ 从文件加载 {symbol} 的K线数据失败: {ex.Message}");
                return null;
            }
        }
    }
}

