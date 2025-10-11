using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 均线距离数据
    /// </summary>
    public class MaDistanceData
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; }
        
        /// <summary>
        /// 24H涨跌幅
        /// </summary>
        public decimal PriceChangePercent { get; set; }
        
        /// <summary>
        /// 24H成交额 (USDT)
        /// </summary>
        public decimal QuoteVolume { get; set; }
        
        /// <summary>
        /// 流通市值 (USDT)
        /// </summary>
        public decimal? CirculatingMarketCap { get; set; }
        
        /// <summary>
        /// 量比 (24H成交额 / 流通市值)
        /// </summary>
        public decimal? VolumeRatio { get; set; }
        
        /// <summary>
        /// N天移动平均线
        /// </summary>
        public decimal MovingAverage { get; set; }
        
        /// <summary>
        /// 距离均线的百分比 (当前价 - 均线) / 均线 * 100%
        /// </summary>
        public decimal DistancePercent { get; set; }
        
        /// <summary>
        /// 是否在均线之上
        /// </summary>
        public bool IsAboveMa => DistancePercent > 0;
        
        /// <summary>
        /// 所属区间
        /// </summary>
        public MaDistanceZone Zone { get; set; }
    }
    
    /// <summary>
    /// 均线距离区间
    /// </summary>
    public enum MaDistanceZone
    {
        /// <summary>
        /// 高于均线，距离 ≤ x%
        /// </summary>
        AboveNear,
        
        /// <summary>
        /// 高于均线，距离 > x%
        /// </summary>
        AboveFar,
        
        /// <summary>
        /// 低于均线，距离大于等于负x%
        /// </summary>
        BelowNear,
        
        /// <summary>
        /// 低于均线，距离小于负x%
        /// </summary>
        BelowFar
    }
    
    /// <summary>
    /// 每日均线距离分布
    /// </summary>
    public class DailyMaDistribution
    {
        /// <summary>
        /// 日期
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// N天周期
        /// </summary>
        public int Period { get; set; }
        
        /// <summary>
        /// 距离阈值 (x%)
        /// </summary>
        public decimal ThresholdPercent { get; set; }
        
        /// <summary>
        /// 低于均线且距离小于-x%的数量
        /// </summary>
        public int BelowFarCount { get; set; }
        
        /// <summary>
        /// 低于均线且距离在-x%到0%之间的数量
        /// </summary>
        public int BelowNearCount { get; set; }
        
        /// <summary>
        /// 高于均线且距离在0%到x%之间的数量
        /// </summary>
        public int AboveNearCount { get; set; }
        
        /// <summary>
        /// 高于均线且距离大于x%的数量
        /// </summary>
        public int AboveFarCount { get; set; }
        
        /// <summary>
        /// 当天所有合约的24H成交额总和 (USDT)
        /// </summary>
        public decimal TotalQuoteVolume { get; set; }
        
        /// <summary>
        /// 总合约数
        /// </summary>
        public int TotalCount => BelowFarCount + BelowNearCount + AboveNearCount + AboveFarCount;
    }
    
    /// <summary>
    /// 均线距离分析结果（单日）
    /// </summary>
    public class MaDistanceAnalysisResult
    {
        /// <summary>
        /// 计算日期
        /// </summary>
        public DateTime Date { get; set; }
        
        /// <summary>
        /// N天周期
        /// </summary>
        public int Period { get; set; }
        
        /// <summary>
        /// 距离阈值 (x%)
        /// </summary>
        public decimal ThresholdPercent { get; set; }
        
        /// <summary>
        /// 所有合约的均线距离数据
        /// </summary>
        public List<MaDistanceData> AllData { get; set; } = new();
        
        /// <summary>
        /// 高于均线且距离 ≤ x% 的合约
        /// </summary>
        public List<MaDistanceData> AboveNear { get; set; } = new();
        
        /// <summary>
        /// 高于均线且距离 > x% 的合约
        /// </summary>
        public List<MaDistanceData> AboveFar { get; set; } = new();
        
        /// <summary>
        /// 低于均线且距离大于等于负x%的合约
        /// </summary>
        public List<MaDistanceData> BelowNear { get; set; } = new();
        
        /// <summary>
        /// 低于均线且距离小于负x%的合约
        /// </summary>
        public List<MaDistanceData> BelowFar { get; set; } = new();
        
        /// <summary>
        /// 获取分布统计
        /// </summary>
        public DailyMaDistribution GetDistribution()
        {
            return new DailyMaDistribution
            {
                Date = Date,
                Period = Period,
                ThresholdPercent = ThresholdPercent,
                AboveNearCount = AboveNear.Count,
                AboveFarCount = AboveFar.Count,
                BelowNearCount = BelowNear.Count,
                BelowFarCount = BelowFar.Count,
                TotalQuoteVolume = AllData.Sum(d => d.QuoteVolume)
            };
        }
    }
    
    /// <summary>
    /// 均线距离历史数据文件
    /// </summary>
    public class MaDistanceHistoryFile
    {
        /// <summary>
        /// 文件版本
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// N天周期
        /// </summary>
        public int Period { get; set; }
        
        /// <summary>
        /// 距离阈值 (x%)
        /// </summary>
        public decimal ThresholdPercent { get; set; }
        
        /// <summary>
        /// 历史每日分布数据
        /// Key: 日期字符串 (yyyy-MM-dd)
        /// Value: 当日分布数据
        /// </summary>
        public Dictionary<string, DailyMaDistribution> DailyDistributions { get; set; } = new();
        
        /// <summary>
        /// 历史每日详细数据（可选，较大）
        /// Key: 日期字符串 (yyyy-MM-dd)
        /// Value: 当日分析结果
        /// </summary>
        public Dictionary<string, MaDistanceAnalysisResult> DailyResults { get; set; } = new();
    }
} 