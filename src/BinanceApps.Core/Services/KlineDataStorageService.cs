using System.Text.Json;
using BinanceApps.Core.Models;
using BinanceApps.Core.Interfaces;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// K线数据本地存储服务 - 支持增量更新和智能缓存
    /// </summary>
    public class KlineDataStorageService
    {
        private readonly string _storageDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public KlineDataStorageService(string storageDirectory = "KlineData")
        {
            _storageDirectory = storageDirectory;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // 确保存储目录存在
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        /// <summary>
        /// 获取K线数据文件路径
        /// </summary>
        private string GetKlineDataFilePath(string symbol)
        {
            return Path.Combine(_storageDirectory, $"{symbol}.json");
        }

        /// <summary>
        /// 保存K线数据到本地文件
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> SaveKlineDataAsync(string symbol, List<Kline> klines)
        {
            try
            {
                var filePath = GetKlineDataFilePath(symbol);
                var klineData = new KlineDataFile
                {
                    Symbol = symbol,
                    LastUpdated = DateTime.UtcNow,
                    Klines = klines
                };

                // 在保存前验证数据完整性
                if (klines.Count > 0)
                {
                    // 检查数据是否按时间排序
                    var sortedKlines = klines.OrderBy(k => k.OpenTime).ToList();
                    klineData.Klines = sortedKlines;
                    
                    Console.WriteLine($"💾 保存 {symbol} K线数据:");
                    Console.WriteLine($"   📊 数据条数: {klines.Count}");
                    Console.WriteLine($"   📅 时间范围: {sortedKlines.First().OpenTime:yyyy-MM-dd} 至 {sortedKlines.Last().OpenTime:yyyy-MM-dd}");
                    Console.WriteLine($"   📈 最高价: {sortedKlines.Max(k => k.HighPrice):F8}");
                    Console.WriteLine($"   📉 最低价: {sortedKlines.Min(k => k.LowPrice):F8}");
                    Console.WriteLine($"   💰 总成交额: {sortedKlines.Sum(k => k.Volume * k.ClosePrice):F2}");
                }

                var json = JsonSerializer.Serialize(klineData, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                
                Console.WriteLine($"✅ 保存 {symbol} K线数据成功，文件大小: {new FileInfo(filePath).Length / 1024.0:F1} KB");
                Console.WriteLine();
                
                return (true, null);
            }
            catch (Exception ex)
            {
                // 详细打印错误信息而不是抛出异常
                Console.WriteLine($"❌ 保存 {symbol} K线数据失败:");
                Console.WriteLine($"   🔍 错误类型: {ex.GetType().Name}");
                Console.WriteLine($"   📝 错误信息: {ex.Message}");
                Console.WriteLine($"   📍 错误位置: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
                Console.WriteLine($"   📁 目标路径: {GetKlineDataFilePath(symbol)}");
                Console.WriteLine();
                
                // 返回失败信息而不是抛出异常
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 从本地文件加载K线数据
        /// </summary>
        public async Task<(List<Kline>? Klines, bool Success, string? ErrorMessage)> LoadKlineDataAsync(string symbol)
        {
            try
            {
                var filePath = GetKlineDataFilePath(symbol);
                if (!File.Exists(filePath))
                {
                    return (null, true, null);
                }

                var json = await File.ReadAllTextAsync(filePath);
                var klineData = JsonSerializer.Deserialize<KlineDataFile>(json, _jsonOptions);
                
                if (klineData?.Klines != null)
                {
                    // 确保每个Kline对象都有正确的Symbol字段
                    foreach (var kline in klineData.Klines)
                    {
                        kline.Symbol = symbol;
                    }
                    
                    Console.WriteLine($"🔍 加载 {symbol} K线数据: {klineData.Klines.Count} 条，第一条Symbol={klineData.Klines.First().Symbol}");
                }
                
                return (klineData?.Klines, true, null);
            }
            catch (Exception ex)
            {
                // 详细打印错误信息而不是抛出异常
                Console.WriteLine($"❌ 加载 {symbol} K线数据失败:");
                Console.WriteLine($"   🔍 错误类型: {ex.GetType().Name}");
                Console.WriteLine($"   📝 错误信息: {ex.Message}");
                Console.WriteLine($"   📍 错误位置: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
                Console.WriteLine($"   📁 文件路径: {GetKlineDataFilePath(symbol)}");
                Console.WriteLine();
                
                // 返回失败信息而不是抛出异常
                return (null, false, ex.Message);
            }
        }

        /// <summary>
        /// 增量更新K线数据 - 智能处理当日未完成数据
        /// </summary>
        public async Task<(bool Success, int NewKlines, int UpdatedKlines, string? ErrorMessage)> IncrementalUpdateKlineDataAsync(
            string symbol, 
            List<Kline> newKlines)
        {
            try
            {
                Console.WriteLine($"🔄 开始增量更新 {symbol} K线数据...");
                
                // 加载现有数据
                var (existingKlines, loadSuccess, loadError) = await LoadKlineDataAsync(symbol);
                if (!loadSuccess)
                {
                    return (false, 0, 0, $"加载现有数据失败: {loadError}");
                }

                // 如果没有现有数据，直接保存新数据
                if (existingKlines == null || existingKlines.Count == 0)
                {
                    Console.WriteLine($"📊 {symbol} 没有现有数据，直接保存新数据");
                    var (saveSuccess, saveError) = await SaveKlineDataAsync(symbol, newKlines);
                    return (saveSuccess, newKlines.Count, 0, saveError);
                }

                // 合并数据逻辑
                var mergedData = await MergeKlineDataAsync(existingKlines, newKlines);
                
                // 保存合并后的数据
                var (success, error) = await SaveKlineDataAsync(symbol, mergedData.MergedKlines);
                
                Console.WriteLine($"✅ {symbol} 增量更新完成:");
                Console.WriteLine($"   📊 新增K线: {mergedData.NewCount} 条");
                Console.WriteLine($"   🔄 更新K线: {mergedData.UpdatedCount} 条");
                Console.WriteLine($"   📈 总K线数: {mergedData.MergedKlines.Count} 条");
                Console.WriteLine();
                
                return (success, mergedData.NewCount, mergedData.UpdatedCount, error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 增量更新 {symbol} K线数据失败: {ex.Message}");
                return (false, 0, 0, ex.Message);
            }
        }

        /// <summary>
        /// 智能下载K线数据 - 只下载缺失的部分，并自动补齐中间缺失的数据
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="apiClient">API客户端</param>
        /// <param name="defaultDays">默认下载天数（本地无数据时）</param>
        public async Task<(bool Success, int DownloadedCount, string? ErrorMessage)> SmartDownloadKlineDataAsync(
            string symbol,
            IBinanceSimulatedApiClient apiClient,
            int defaultDays = 90)
        {
            try
            {
                // 1. 检查本地数据
                var (existingKlines, loadSuccess, loadError) = await LoadKlineDataAsync(symbol);
                
                DateTime startDate;
                
                if (loadSuccess && existingKlines != null && existingKlines.Count > 0)
                {
                    // 有本地数据 - 检查是否有缺失
                    var sortedDates = existingKlines
                        .Select(k => k.OpenTime.Date)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                    
                    var lastDate = sortedDates.Last();
                    var firstDate = sortedDates.First();
                    
                    // 检查数据连续性，找到第一个缺失的日期
                    DateTime? firstGapDate = null;
                    for (int i = 0; i < sortedDates.Count - 1; i++)
                    {
                        var currentDate = sortedDates[i];
                        var nextDate = sortedDates[i + 1];
                        var expectedNextDate = currentDate.AddDays(1);
                        
                        // 如果下一个日期不是连续的，说明有缺失
                        if (nextDate > expectedNextDate)
                        {
                            firstGapDate = expectedNextDate;
                            var gapDays = (nextDate - currentDate).Days - 1;
                            Console.WriteLine($"⚠️ 发现数据缺失: {currentDate:yyyy-MM-dd} 到 {nextDate:yyyy-MM-dd} 之间缺失 {gapDays} 天");
                            break;
                        }
                    }
                    
                    if (firstGapDate.HasValue)
                    {
                        // 有缺失 - 从缺失的前一天开始下载，确保补齐中间数据
                        startDate = firstGapDate.Value.AddDays(-1);
                        Console.WriteLine($"📊 {symbol} 检测到数据缺失");
                        Console.WriteLine($"📊 本地数据范围: {firstDate:yyyy-MM-dd} 至 {lastDate:yyyy-MM-dd}");
                        Console.WriteLine($"📥 将从 {startDate:yyyy-MM-dd} 开始补齐缺失数据到今天");
                    }
                    else
                    {
                        // 无缺失 - 从最新数据的日期开始下载
                        startDate = lastDate; // 包含最后一天（可能不完整）
                        Console.WriteLine($"📊 {symbol} 本地最新数据: {lastDate:yyyy-MM-dd}");
                        Console.WriteLine($"📥 将下载从 {startDate:yyyy-MM-dd} 到今天的数据");
                    }
                }
                else
                {
                    // 没有本地数据 - 下载默认天数
                    startDate = DateTime.Today.AddDays(-defaultDays + 1);
                    
                    Console.WriteLine($"📊 {symbol} 本地无数据");
                    Console.WriteLine($"📥 将下载最近 {defaultDays} 天的数据");
                }
                
                // 2. 检查是否需要下载
                var daysToDownload = (DateTime.Today - startDate).Days + 1;
                
                if (daysToDownload <= 0)
                {
                    Console.WriteLine($"✅ {symbol} 数据已是最新，无需下载");
                    return (true, 0, null);
                }
                
                Console.WriteLine($"📈 需要下载 {daysToDownload} 天的数据");
                
                // 3. 调用API下载（使用时间范围）
                List<Kline> newKlines;
                
                // 检查API客户端类型，选择合适的调用方式
                var apiClientType = apiClient.GetType();
                var hasTimeRangeMethod = apiClientType.GetMethod("GetKlinesAsync", 
                    new Type[] { typeof(string), typeof(KlineInterval), typeof(DateTime), typeof(DateTime?), typeof(int) });
                
                if (hasTimeRangeMethod != null)
                {
                    // 使用新的时间范围方法（支持反射调用）
                    try
                    {
                        var taskObject = hasTimeRangeMethod.Invoke(apiClient, new object[] 
                        { 
                            symbol, 
                            KlineInterval.OneDay, 
                            startDate,
                            DateTime.Today.AddDays(1), // 包含今天
                            Math.Min(daysToDownload + 5, 1000) // 稍微多下载几天以防万一
                        });
                        
                        if (taskObject is Task<List<Kline>> task)
                        {
                            newKlines = await task;
                        }
                        else
                        {
                            throw new InvalidOperationException("反射调用返回类型不匹配");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ 使用时间范围方法失败，降级到原有方法: {ex.Message}");
                        var limit = Math.Min(daysToDownload + 5, 1000);
                        newKlines = await apiClient.GetKlinesAsync(symbol, KlineInterval.OneDay, limit);
                    }
                }
                else
                {
                    // 降级使用原有方法
                    var limit = Math.Min(daysToDownload + 5, 1000);
                    newKlines = await apiClient.GetKlinesAsync(symbol, KlineInterval.OneDay, limit);
                }
                
                if (newKlines == null || newKlines.Count == 0)
                {
                    return (false, 0, "API返回空数据");
                }
                
                Console.WriteLine($"📥 从API获取到 {newKlines.Count} 条K线数据");
                
                // 4. 增量更新本地数据
                var (updateSuccess, newCount, updatedCount, updateError) = 
                    await IncrementalUpdateKlineDataAsync(symbol, newKlines);
                
                if (updateSuccess)
                {
                    var totalChanges = newCount + updatedCount;
                    Console.WriteLine($"✅ {symbol} 数据更新成功: 新增{newCount}条, 更新{updatedCount}条");
                    return (true, totalChanges, null);
                }
                else
                {
                    return (false, 0, updateError);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {symbol} 智能下载失败: {ex.Message}");
                return (false, 0, ex.Message);
            }
        }

        /// <summary>
        /// 合并K线数据 - 智能处理当日未完成数据
        /// </summary>
        private async Task<KlineMergeResult> MergeKlineDataAsync(List<Kline> existingKlines, List<Kline> newKlines)
        {
            await Task.CompletedTask; // 标记为异步方法

            var merged = new List<Kline>(existingKlines);
            var newCount = 0;
            var updatedCount = 0;
            var today = DateTime.UtcNow.Date;
            
            // 找到本地最后一条K线的日期（用于智能更新判断）
            var lastLocalDate = existingKlines.Count > 0 
                ? existingKlines.Max(k => k.OpenTime).Date 
                : DateTime.MinValue;

            Console.WriteLine($"🔄 合并K线数据:");
            Console.WriteLine($"   📊 现有数据: {existingKlines.Count} 条");
            Console.WriteLine($"   📊 新数据: {newKlines.Count} 条");
            if (lastLocalDate != DateTime.MinValue)
            {
                Console.WriteLine($"   📅 本地最后日期: {lastLocalDate:yyyy-MM-dd}");
            }

            foreach (var newKline in newKlines)
            {
                var klineDate = newKline.OpenTime.Date;
                var existingKline = merged.FirstOrDefault(k => k.OpenTime.Date == klineDate);

                if (existingKline == null)
                {
                    // 新的K线数据
                    merged.Add(newKline);
                    newCount++;
                    Console.WriteLine($"   ➕ 新增: {klineDate:yyyy-MM-dd}");
                }
                else
                {
                    // 检查是否需要更新
                    bool shouldUpdate = false;
                    var yesterday = today.AddDays(-1);
                    
                    if (klineDate == today)
                    {
                        // 当日数据：始终更新（因为数据可能不完整）
                        shouldUpdate = true;
                        Console.WriteLine($"   🔄 更新当日数据: {klineDate:yyyy-MM-dd}");
                    }
                    else if (klineDate == yesterday)
                    {
                        // 昨日数据：也需要更新（因为可能是之前的"当日数据"，不完整）
                        shouldUpdate = true;
                        Console.WriteLine($"   🔄 更新昨日数据: {klineDate:yyyy-MM-dd} (可能之前是不完整的当日数据)");
                    }
                    else if (klineDate == lastLocalDate)
                    {
                        // 本地最后一条K线：始终更新（确保数据完整性）
                        // 这条逻辑确保即使是周五下午下载的数据，周一重新下载时也会更新
                        shouldUpdate = true;
                        Console.WriteLine($"   🔄 更新本地最后一条K线: {klineDate:yyyy-MM-dd} (确保数据完整)");
                    }
                    else if (IsDataDifferent(existingKline, newKline))
                    {
                        // 其他历史数据：仅在数据不同时更新
                        shouldUpdate = true;
                        Console.WriteLine($"   🔄 更新历史数据: {klineDate:yyyy-MM-dd}");
                    }

                    if (shouldUpdate)
                    {
                        // 更新现有K线数据
                        merged.Remove(existingKline);
                        merged.Add(newKline);
                        updatedCount++;
                    }
                }
            }

            // 按时间排序
            merged = merged.OrderBy(k => k.OpenTime).ToList();
            
            return new KlineMergeResult
            {
                MergedKlines = merged,
                NewCount = newCount,
                UpdatedCount = updatedCount
            };
        }

        /// <summary>
        /// 检查两个K线数据是否不同
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
        /// 检查K线数据是否需要更新
        /// </summary>
        public async Task<KlineUpdateStatus> CheckUpdateStatusAsync(string symbol)
        {
            try
            {
                var filePath = GetKlineDataFilePath(symbol);
                if (!File.Exists(filePath))
                {
                    return new KlineUpdateStatus
                    {
                        NeedsUpdate = true,
                        Reason = "文件不存在",
                        LastKlineDate = null,
                        IsToday = false
                    };
                }

                var (existingKlines, success, error) = await LoadKlineDataAsync(symbol);
                if (!success || existingKlines == null || existingKlines.Count == 0)
                {
                    return new KlineUpdateStatus
                    {
                        NeedsUpdate = true,
                        Reason = "无法加载现有数据",
                        LastKlineDate = null,
                        IsToday = false
                    };
                }

                var lastKline = existingKlines.OrderByDescending(k => k.OpenTime).First();
                var lastKlineDate = lastKline.OpenTime.Date;
                var today = DateTime.UtcNow.Date;
                var yesterday = today.AddDays(-1);

                if (lastKlineDate == today)
                {
                    return new KlineUpdateStatus
                    {
                        NeedsUpdate = true,
                        Reason = "当日数据需要更新",
                        LastKlineDate = lastKlineDate,
                        IsToday = true
                    };
                }
                else if (lastKlineDate == yesterday)
                {
                    return new KlineUpdateStatus
                    {
                        NeedsUpdate = true,
                        Reason = "昨日数据需要更新 (可能之前是不完整的当日数据)",
                        LastKlineDate = lastKlineDate,
                        IsToday = false
                    };
                }
                else if (lastKlineDate < yesterday)
                {
                    return new KlineUpdateStatus
                    {
                        NeedsUpdate = true,
                        Reason = $"数据过期 (最后: {lastKlineDate:yyyy-MM-dd})",
                        LastKlineDate = lastKlineDate,
                        IsToday = false
                    };
                }
                else
                {
                    return new KlineUpdateStatus
                    {
                        NeedsUpdate = false,
                        Reason = "数据已是最新",
                        LastKlineDate = lastKlineDate,
                        IsToday = false
                    };
                }
            }
            catch (Exception ex)
            {
                return new KlineUpdateStatus
                {
                    NeedsUpdate = true,
                    Reason = $"检查失败: {ex.Message}",
                    LastKlineDate = null,
                    IsToday = false
                };
            }
        }

        /// <summary>
        /// 检查K线数据是否存在且是否过期
        /// </summary>
        public async Task<(bool Exists, bool IsExpired, DateTime? LastUpdated)> CheckKlineDataStatusAsync(string symbol, TimeSpan maxAge)
        {
            try
            {
                var filePath = GetKlineDataFilePath(symbol);
                if (!File.Exists(filePath))
                {
                    return (false, false, null);
                }

                var json = await File.ReadAllTextAsync(filePath);
                var klineData = JsonSerializer.Deserialize<KlineDataFile>(json, _jsonOptions);
                
                if (klineData?.LastUpdated == null)
                {
                    return (true, true, null);
                }

                var age = DateTime.UtcNow - klineData.LastUpdated;
                var isExpired = age > maxAge;

                return (true, isExpired, klineData.LastUpdated);
            }
            catch
            {
                return (false, false, null);
            }
        }

        /// <summary>
        /// 删除过期的K线数据文件
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> CleanupExpiredDataAsync(TimeSpan maxAge)
        {
            try
            {
                var files = Directory.GetFiles(_storageDirectory, "*.json");
                var deletedCount = 0;

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var klineData = JsonSerializer.Deserialize<KlineDataFile>(json, _jsonOptions);
                        
                        if (klineData?.LastUpdated != null)
                        {
                            var age = DateTime.UtcNow - klineData.LastUpdated;
                            if (age > maxAge)
                            {
                                File.Delete(file);
                                deletedCount++;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略损坏的文件
                        continue;
                    }
                }

                if (deletedCount > 0)
                {
                    Console.WriteLine($"🗑️ 清理过期K线数据文件: {deletedCount} 个");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清理过期数据失败: {ex.Message}");
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 删除所有K线数据文件
        /// </summary>
        public Task<(bool Success, string? ErrorMessage)> DeleteAllKlineDataAsync()
        {
            try
            {
                if (!Directory.Exists(_storageDirectory))
                {
                    return Task.FromResult((true, (string?)null));
                }

                var files = Directory.GetFiles(_storageDirectory, "*.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }

                Console.WriteLine($"🗑️ 已删除 {files.Length} 个K线数据文件");
                return Task.FromResult((true, (string?)null));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 删除K线数据文件失败: {ex.Message}");
                return Task.FromResult((false, (string?)ex.Message));
            }
        }

        /// <summary>
        /// 获取存储的K线数据信息
        /// </summary>
        public async Task<(List<KlineDataFileInfo>? FileInfos, bool Success, string? ErrorMessage)> GetStorageInfoAsync()
        {
            try
            {
                if (!Directory.Exists(_storageDirectory))
                {
                    return (new List<KlineDataFileInfo>(), true, null);
                }

                var files = Directory.GetFiles(_storageDirectory, "*.json");
                var result = new List<KlineDataFileInfo>();

                foreach (var file in files)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file);
                        var klineData = JsonSerializer.Deserialize<KlineDataFile>(json, _jsonOptions);
                        
                        if (klineData != null)
                        {
                            var fileInfo = new FileInfo(file);
                            result.Add(new KlineDataFileInfo
                            {
                                Symbol = klineData.Symbol,
                                LastUpdated = klineData.LastUpdated,
                                FileSize = fileInfo.Length,
                                KlineCount = klineData.Klines?.Count ?? 0
                            });
                        }
                    }
                    catch
                    {
                        // 忽略损坏的文件
                        continue;
                    }
                }

                return (result.OrderBy(x => x.Symbol).ToList(), true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取存储数据信息失败:");
                Console.WriteLine($"   🔍 错误类型: {ex.GetType().Name}");
                Console.WriteLine($"   📝 错误信息: {ex.Message}");
                Console.WriteLine($"   📍 错误位置: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
                Console.WriteLine();
                return (null, false, ex.Message);
            }
        }
    }

    /// <summary>
    /// K线数据文件结构
    /// </summary>
    public class KlineDataFile
    {
        public string Symbol { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public List<Kline> Klines { get; set; } = new();
    }

    /// <summary>
    /// K线数据文件信息
    /// </summary>
    public class KlineDataFileInfo
    {
        public string Symbol { get; set; } = "";
        public DateTime LastUpdated { get; set; }
        public long FileSize { get; set; }
        public int KlineCount { get; set; }
    }

    /// <summary>
    /// K线数据合并结果
    /// </summary>
    public class KlineMergeResult
    {
        public List<Kline> MergedKlines { get; set; } = new();
        public int NewCount { get; set; }
        public int UpdatedCount { get; set; }
    }

    /// <summary>
    /// K线数据更新状态
    /// </summary>
    public class KlineUpdateStatus
    {
        public bool NeedsUpdate { get; set; }
        public string Reason { get; set; } = "";
        public DateTime? LastKlineDate { get; set; }
        public bool IsToday { get; set; }
    }
} 