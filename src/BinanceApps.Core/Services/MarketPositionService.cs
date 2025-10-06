using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 市场位置数据服务
    /// </summary>
    public class MarketPositionService
    {
        private readonly ILogger<MarketPositionService> _logger;
        private string _dataFilePath;
        private MarketPositionHistoryFile? _historyData;
        
        public MarketPositionService(ILogger<MarketPositionService> logger)
        {
            _logger = logger;
            
            // 确保Data目录存在
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }
            
            // 初始化时不设置文件路径，将在GetOrCalculateRecentDaysAsync中根据天数动态设置
            _dataFilePath = "";
        }
        
        /// <summary>
        /// 初始化服务，加载历史数据
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await LoadHistoryDataAsync();
                _logger.LogInformation("市场位置数据服务初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "市场位置数据服务初始化失败");
                _historyData = new MarketPositionHistoryFile();
            }
        }
        
        /// <summary>
        /// 加载历史数据
        /// </summary>
        private async Task LoadHistoryDataAsync()
        {
            if (!File.Exists(_dataFilePath))
            {
                _logger.LogInformation("历史数据文件不存在，创建新的数据文件");
                _historyData = new MarketPositionHistoryFile();
                await SaveHistoryDataAsync();
                return;
            }
            
            try
            {
                var jsonContent = await File.ReadAllTextAsync(_dataFilePath);
                _historyData = JsonSerializer.Deserialize<MarketPositionHistoryFile>(jsonContent);
                
                if (_historyData == null)
                {
                    _logger.LogWarning("历史数据文件解析失败，创建新的数据文件");
                    _historyData = new MarketPositionHistoryFile();
                }
                else
                {
                    _logger.LogInformation($"成功加载历史数据，包含 {_historyData.DailyPositions.Count} 天的记录");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载历史数据失败，创建新的数据文件");
                _historyData = new MarketPositionHistoryFile();
            }
        }
        
        /// <summary>
        /// 保存历史数据
        /// </summary>
        private async Task SaveHistoryDataAsync()
        {
            try
            {
                if (_historyData == null) return;
                
                _historyData.LastUpdated = DateTime.UtcNow;
                
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var jsonContent = JsonSerializer.Serialize(_historyData, options);
                await File.WriteAllTextAsync(_dataFilePath, jsonContent);
                
                _logger.LogInformation($"历史数据已保存，包含 {_historyData.DailyPositions.Count} 天的记录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存历史数据失败");
            }
        }
        
        /// <summary>
        /// 获取或计算最近N天的市场位置数据
        /// </summary>
        public async Task<List<DailyMarketPosition>> GetOrCalculateRecentDaysAsync(
            int days, 
            int analysisDays, 
            Func<DateTime, int, Task<List<LocationData>>> calculateLocationDataFunc)
        {
            // 根据分析天数设置不同的文件路径
            var dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            _dataFilePath = Path.Combine(dataDirectory, $"market_position_history_{analysisDays}days.json");
            
            if (_historyData == null || _historyData.AnalysisDays != analysisDays)
            {
                await InitializeAsync();
                if (_historyData != null)
                {
                    _historyData.AnalysisDays = analysisDays;
                }
            }
            
            var result = new List<DailyMarketPosition>();
            var today = DateTime.UtcNow.Date;
            
            // 计算需要的日期范围（不包括今天，因为今天的数据只用于显示）
            var endDate = today.AddDays(-1); // 昨天
            var startDate = endDate.AddDays(-(days - 1)); // 往前推N-1天
            
            _logger.LogInformation($"计算市场位置数据范围: {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd}");
            
            // 检查每一天的数据
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var existingData = _historyData?.GetPositionByDate(date);
                
                if (existingData != null && existingData.IsValid)
                {
                    // 使用已有数据
                    result.Add(existingData);
                    _logger.LogDebug($"使用已有数据: {date:yyyy-MM-dd}");
                }
                else
                {
                    // 计算新数据
                    _logger.LogInformation($"计算新数据: {date:yyyy-MM-dd}");
                    var newData = await CalculateDailyMarketPositionAsync(date, analysisDays, calculateLocationDataFunc);
                    
                    if (newData.IsValid)
                    {
                        result.Add(newData);
                        
                        // 保存到历史数据
                        _historyData?.AddOrUpdatePosition(newData);
                        await SaveHistoryDataAsync();
                        
                        _logger.LogInformation($"新数据计算完成: {newData.DisplayText}");
                    }
                    else
                    {
                        _logger.LogWarning($"无法计算 {date:yyyy-MM-dd} 的数据");
                    }
                }
            }
            
            return result.OrderBy(r => r.Date).ToList();
        }
        
        /// <summary>
        /// 计算指定日期的市场位置统计
        /// </summary>
        private async Task<DailyMarketPosition> CalculateDailyMarketPositionAsync(
            DateTime date, 
            int analysisDays, 
            Func<DateTime, int, Task<List<LocationData>>> calculateLocationDataFunc)
        {
            try
            {
                // 调用外部函数计算位置数据
                var locationData = await calculateLocationDataFunc(date, analysisDays);
                
                return CalculatePositionCounts(date, locationData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算 {date:yyyy-MM-dd} 市场位置失败");
                return new DailyMarketPosition { Date = date };
            }
        }
        
        /// <summary>
        /// 根据位置数据计算各区间的合约数量
        /// </summary>
        public static DailyMarketPosition CalculatePositionCounts(DateTime date, List<LocationData> locationData)
        {
            var result = new DailyMarketPosition { Date = date };
            
            foreach (var location in locationData)
            {
                var positionPercent = location.LocationRatio * 100;
                
                if (positionPercent <= 25)
                {
                    result.LowPositionCount++;
                }
                else if (positionPercent <= 50)
                {
                    result.MidLowPositionCount++;
                }
                else if (positionPercent <= 75)
                {
                    result.MidHighPositionCount++;
                }
                else
                {
                    result.HighPositionCount++;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public string GetCacheStats()
        {
            if (_historyData == null) return "数据未初始化";
            
            return $"历史记录: {_historyData.DailyPositions.Count} 天, " +
                   $"最后更新: {_historyData.LastUpdated:yyyy-MM-dd HH:mm:ss}";
        }
        
        /// <summary>
        /// 清理过期数据（保留最近90天）
        /// </summary>
        public async Task CleanupExpiredDataAsync(int keepDays = 90)
        {
            if (_historyData == null) return;
            
            var cutoffDate = DateTime.UtcNow.Date.AddDays(-keepDays);
            var originalCount = _historyData.DailyPositions.Count;
            
            _historyData.DailyPositions = _historyData.DailyPositions
                .Where(p => p.Date >= cutoffDate)
                .ToList();
            
            var removedCount = originalCount - _historyData.DailyPositions.Count;
            
            if (removedCount > 0)
            {
                await SaveHistoryDataAsync();
                _logger.LogInformation($"清理过期数据完成，删除 {removedCount} 条记录，保留 {_historyData.DailyPositions.Count} 条记录");
            }
        }
    }
} 