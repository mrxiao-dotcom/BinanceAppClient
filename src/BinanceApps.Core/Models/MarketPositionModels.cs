using System;
using System.Collections.Generic;
using System.Linq;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 每日市场位置统计数据
    /// </summary>
    public class DailyMarketPosition
    {
        /// <summary>
        /// 日期
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// 低位区域合约数量 (0-25%)
        /// </summary>
        public int LowPositionCount { get; set; }
        
        /// <summary>
        /// 中低位区域合约数量 (26-50%)
        /// </summary>
        public int MidLowPositionCount { get; set; }
        
        /// <summary>
        /// 中高位区域合约数量 (51-75%)
        /// </summary>
        public int MidHighPositionCount { get; set; }
        
        /// <summary>
        /// 高位区域合约数量 (76%以上)
        /// </summary>
        public int HighPositionCount { get; set; }
        
        /// <summary>
        /// 总合约数量
        /// </summary>
        public int TotalCount => LowPositionCount + MidLowPositionCount + MidHighPositionCount + HighPositionCount;
        
        /// <summary>
        /// 数据是否有效
        /// </summary>
        public bool IsValid => TotalCount > 0;
        
        /// <summary>
        /// 格式化显示文本
        /// </summary>
        public string DisplayText => $"{Date:yyyy-MM-dd}  {LowPositionCount},{MidLowPositionCount},{MidHighPositionCount},{HighPositionCount}";
    }
    
    /// <summary>
    /// 市场位置历史记录文件
    /// </summary>
    public class MarketPositionHistoryFile
    {
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// 数据版本
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// 分析天数
        /// </summary>
        public int AnalysisDays { get; set; }
        
        /// <summary>
        /// 每日市场位置统计数据
        /// </summary>
        public List<DailyMarketPosition> DailyPositions { get; set; } = new List<DailyMarketPosition>();
        
        /// <summary>
        /// 获取指定日期的数据
        /// </summary>
        public DailyMarketPosition? GetPositionByDate(DateTime date)
        {
            return DailyPositions.FirstOrDefault(p => p.Date.Date == date.Date);
        }
        
        /// <summary>
        /// 添加或更新指定日期的数据
        /// </summary>
        public void AddOrUpdatePosition(DailyMarketPosition position)
        {
            var existing = GetPositionByDate(position.Date);
            if (existing != null)
            {
                existing.LowPositionCount = position.LowPositionCount;
                existing.MidLowPositionCount = position.MidLowPositionCount;
                existing.MidHighPositionCount = position.MidHighPositionCount;
                existing.HighPositionCount = position.HighPositionCount;
            }
            else
            {
                DailyPositions.Add(position);
            }
            
            // 按日期排序，最新的在前面
            DailyPositions = DailyPositions.OrderByDescending(p => p.Date).ToList();
        }
        
        /// <summary>
        /// 获取最近N天的数据
        /// </summary>
        public List<DailyMarketPosition> GetRecentDays(int days)
        {
            return DailyPositions
                .OrderByDescending(p => p.Date)
                .Take(days)
                .OrderBy(p => p.Date)
                .ToList();
        }
    }
} 