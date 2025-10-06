using System.Text.Json;
using BinanceApps.Core.Models;

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
        /// 合并K线数据 - 智能处理当日未完成数据
        /// </summary>
        private async Task<KlineMergeResult> MergeKlineDataAsync(List<Kline> existingKlines, List<Kline> newKlines)
        {
            await Task.CompletedTask; // 标记为异步方法

            var merged = new List<Kline>(existingKlines);
            var newCount = 0;
            var updatedCount = 0;
            var today = DateTime.UtcNow.Date;

            Console.WriteLine($"🔄 合并K线数据:");
            Console.WriteLine($"   📊 现有数据: {existingKlines.Count} 条");
            Console.WriteLine($"   📊 新数据: {newKlines.Count} 条");

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