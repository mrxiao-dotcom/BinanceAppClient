using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 市场信号类型
    /// </summary>
    public enum MarketSignal
    {
        /// <summary>
        /// 牛市信号
        /// </summary>
        Bullish,
        
        /// <summary>
        /// 熊市信号
        /// </summary>
        Bearish,
        
        /// <summary>
        /// 中性信号
        /// </summary>
        Neutral
    }
    
    /// <summary>
    /// 市场趋势类型
    /// </summary>
    public enum MarketTrend
    {
        /// <summary>
        /// 强牛市
        /// </summary>
        StrongBullish,
        
        /// <summary>
        /// 牛市
        /// </summary>
        Bullish,
        
        /// <summary>
        /// 震荡市
        /// </summary>
        Sideways,
        
        /// <summary>
        /// 熊市
        /// </summary>
        Bearish,
        
        /// <summary>
        /// 强熊市/底部
        /// </summary>
        StrongBearish
    }
    
    /// <summary>
    /// 仪表板综合数据
    /// </summary>
    public class DashboardSummary
    {
        /// <summary>
        /// 数据更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 市场趋势分析
        /// </summary>
        public MarketTrendAnalysis TrendAnalysis { get; set; } = new();
        
        /// <summary>
        /// 高低价位置统计
        /// </summary>
        public PositionDistribution PositionStats { get; set; } = new();
        
        /// <summary>
        /// 24小时市场动态
        /// </summary>
        public MarketDynamics MarketStats { get; set; } = new();
        
        /// <summary>
        /// 均线距离统计
        /// </summary>
        public MaDistanceDistribution MaStats { get; set; } = new();
        
        /// <summary>
        /// 量比排行TOP20（成交额/流通市值）
        /// </summary>
        public List<VolumeRatioItem> VolumeRatioTop20 { get; set; } = new();
        
        /// <summary>
        /// 30天从最低价涨幅TOP20
        /// </summary>
        public List<PriceChangeFrom30DayLowItem> Top20GainsFrom30DayLow { get; set; } = new();
        
        /// <summary>
        /// 30天从最高价跌幅TOP20
        /// </summary>
        public List<PriceChangeFrom30DayHighItem> Top20FallsFrom30DayHigh { get; set; } = new();
    }
    
    /// <summary>
    /// 市场趋势综合分析
    /// </summary>
    public class MarketTrendAnalysis
    {
        /// <summary>
        /// 均线信号
        /// </summary>
        public SignalDetail MaSignal { get; set; } = new();
        
        /// <summary>
        /// 位置信号
        /// </summary>
        public SignalDetail PositionSignal { get; set; } = new();
        
        /// <summary>
        /// 涨跌信号
        /// </summary>
        public SignalDetail ChangeSignal { get; set; } = new();
        
        /// <summary>
        /// 波动信号
        /// </summary>
        public SignalDetail VolatilitySignal { get; set; } = new();
        
        /// <summary>
        /// 综合趋势
        /// </summary>
        public MarketTrend OverallTrend { get; set; }
        
        /// <summary>
        /// 牛市信号数量
        /// </summary>
        public int BullishSignalCount { get; set; }
        
        /// <summary>
        /// 操作建议
        /// </summary>
        public List<string> Suggestions { get; set; } = new();
        
        /// <summary>
        /// 趋势描述
        /// </summary>
        public string TrendDescription => OverallTrend switch
        {
            MarketTrend.StrongBullish => "强牛市 🚀",
            MarketTrend.Bullish => "牛市 🐂",
            MarketTrend.Sideways => "震荡市 ⚖️",
            MarketTrend.Bearish => "熊市 🐻",
            MarketTrend.StrongBearish => "强熊市/底部 ⚠️",
            _ => "未知"
        };
        
        /// <summary>
        /// 趋势图标
        /// </summary>
        public string TrendIcon => OverallTrend switch
        {
            MarketTrend.StrongBullish => "🚀",
            MarketTrend.Bullish => "🐂",
            MarketTrend.Sideways => "⚖️",
            MarketTrend.Bearish => "🐻",
            MarketTrend.StrongBearish => "⚠️",
            _ => "❓"
        };
    }
    
    /// <summary>
    /// 信号详情
    /// </summary>
    public class SignalDetail
    {
        /// <summary>
        /// 信号名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 信号类型
        /// </summary>
        public MarketSignal Signal { get; set; }
        
        /// <summary>
        /// 信号描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// 原始数据描述
        /// </summary>
        public string RawData { get; set; } = string.Empty;
        
        /// <summary>
        /// 信号图标
        /// </summary>
        public string SignalIcon => Signal switch
        {
            MarketSignal.Bullish => "🟢",
            MarketSignal.Bearish => "🔴",
            MarketSignal.Neutral => "🟡",
            _ => "⚪"
        };
        
        /// <summary>
        /// 信号文本
        /// </summary>
        public string SignalText => Signal switch
        {
            MarketSignal.Bullish => "牛市",
            MarketSignal.Bearish => "熊市",
            MarketSignal.Neutral => "中性",
            _ => "未知"
        };
    }
    
    /// <summary>
    /// 高低价位置分布
    /// </summary>
    public class PositionDistribution
    {
        /// <summary>
        /// 高区数量
        /// </summary>
        public int HighCount { get; set; }
        
        /// <summary>
        /// 中高区数量
        /// </summary>
        public int MidHighCount { get; set; }
        
        /// <summary>
        /// 中低区数量
        /// </summary>
        public int MidLowCount { get; set; }
        
        /// <summary>
        /// 低区数量
        /// </summary>
        public int LowCount { get; set; }
        
        /// <summary>
        /// 总数量
        /// </summary>
        public int TotalCount => HighCount + MidHighCount + MidLowCount + LowCount;
        
        /// <summary>
        /// 高位比例（高+中高）
        /// </summary>
        public decimal HighRatio => TotalCount > 0 ? (decimal)(HighCount + MidHighCount) / TotalCount * 100 : 0;
        
        /// <summary>
        /// 低位比例（低+中低）
        /// </summary>
        public decimal LowRatio => TotalCount > 0 ? (decimal)(LowCount + MidLowCount) / TotalCount * 100 : 0;
    }
    
    /// <summary>
    /// 24小时市场动态
    /// </summary>
    public class MarketDynamics
    {
        /// <summary>
        /// 总成交额（USDT）
        /// </summary>
        public decimal TotalVolume { get; set; }
        
        /// <summary>
        /// 总成交额格式化显示
        /// </summary>
        public string TotalVolumeDisplay => TotalVolume >= 1_000_000_000 
            ? $"${TotalVolume / 1_000_000_000:F2}B"
            : $"${TotalVolume / 1_000_000:F1}M";
        
        /// <summary>
        /// 成交额位置（相对历史水平）
        /// </summary>
        public string VolumePosition { get; set; } = "中等";
        
        /// <summary>
        /// 上涨合约数量
        /// </summary>
        public int RisingCount { get; set; }
        
        /// <summary>
        /// 下跌合约数量
        /// </summary>
        public int FallingCount { get; set; }
        
        /// <summary>
        /// 平盘合约数量
        /// </summary>
        public int FlatCount { get; set; }
        
        /// <summary>
        /// 上涨比例
        /// </summary>
        public decimal RisingRatio => (RisingCount + FallingCount) > 0 
            ? (decimal)RisingCount / (RisingCount + FallingCount) * 100 
            : 0;
        
        /// <summary>
        /// 24H最大涨幅TOP5合约列表
        /// </summary>
        public List<VolatilityItem> TopGainers { get; set; } = new();
        
        /// <summary>
        /// 24H最大跌幅TOP5合约列表
        /// </summary>
        public List<VolatilityItem> TopLosers { get; set; } = new();
        
        /// <summary>
        /// 高波动合约数量（绝对值>3%）
        /// </summary>
        public int HighVolatilityCount { get; set; }
        
        /// <summary>
        /// 总合约数
        /// </summary>
        public int TotalSymbolCount { get; set; }
        
        /// <summary>
        /// 高波动比例
        /// </summary>
        public decimal HighVolatilityRatio => TotalSymbolCount > 0 
            ? (decimal)HighVolatilityCount / TotalSymbolCount * 100 
            : 0;
    }
    
    /// <summary>
    /// 波动率项目
    /// </summary>
    public class VolatilityItem
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 24H涨跌幅
        /// </summary>
        public decimal ChangePercent { get; set; }
        
        /// <summary>
        /// 波动率（绝对值）
        /// </summary>
        public decimal Volatility => Math.Abs(ChangePercent);
        
        /// <summary>
        /// 格式化显示
        /// </summary>
        public string Display => $"{Symbol}: {(ChangePercent >= 0 ? "+" : "")}{ChangePercent:F2}%";
        
        /// <summary>
        /// 图标
        /// </summary>
        public string Icon => Volatility > 5 ? "🔥" : Volatility > 3 ? "📈" : "📊";
    }
    
    /// <summary>
    /// 均线距离分布
    /// </summary>
    public class MaDistanceDistribution
    {
        /// <summary>
        /// 周期（N天）
        /// </summary>
        public int Period { get; set; }
        
        /// <summary>
        /// 阈值（x%）
        /// </summary>
        public decimal Threshold { get; set; }
        
        /// <summary>
        /// 高于均线且距离>x%的数量
        /// </summary>
        public int AboveFarCount { get; set; }
        
        /// <summary>
        /// 高于均线且距离0~x%的数量
        /// </summary>
        public int AboveNearCount { get; set; }
        
        /// <summary>
        /// 低于均线且距离-x~0%的数量
        /// </summary>
        public int BelowNearCount { get; set; }
        
        /// <summary>
        /// 低于均线且距离小于-x%的数量
        /// </summary>
        public int BelowFarCount { get; set; }
        
        /// <summary>
        /// 总数量
        /// </summary>
        public int TotalCount => AboveFarCount + AboveNearCount + BelowNearCount + BelowFarCount;
        
        /// <summary>
        /// 均线之上比例
        /// </summary>
        public decimal AboveRatio => TotalCount > 0 
            ? (decimal)(AboveFarCount + AboveNearCount) / TotalCount * 100 
            : 0;
        
        /// <summary>
        /// 均线之下比例
        /// </summary>
        public decimal BelowRatio => TotalCount > 0 
            ? (decimal)(BelowFarCount + BelowNearCount) / TotalCount * 100 
            : 0;
    }
    
    /// <summary>
    /// 量比排行项目
    /// </summary>
    public class VolumeRatioItem
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 24H成交额（USDT）
        /// </summary>
        public decimal QuoteVolume { get; set; }
        
        /// <summary>
        /// 流通市值（USDT）
        /// </summary>
        public decimal CirculatingMarketCap { get; set; }
        
        /// <summary>
        /// 量比（成交额/流通市值）
        /// </summary>
        public decimal VolumeRatio { get; set; }
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; }
        
        /// <summary>
        /// 24H涨跌幅
        /// </summary>
        public decimal PriceChangePercent { get; set; }
        
        /// <summary>
        /// 量比百分比显示
        /// </summary>
        public string VolumeRatioDisplay => $"{VolumeRatio * 100:F2}%";
        
        /// <summary>
        /// 成交额显示
        /// </summary>
        public string QuoteVolumeDisplay => QuoteVolume >= 1_000_000_000 
            ? $"${QuoteVolume / 1_000_000_000:F2}B"
            : $"${QuoteVolume / 1_000_000:F1}M";
        
        /// <summary>
        /// 流通市值显示
        /// </summary>
        public string MarketCapDisplay => CirculatingMarketCap >= 1_000_000_000 
            ? $"${CirculatingMarketCap / 1_000_000_000:F2}B"
            : $"${CirculatingMarketCap / 1_000_000:F1}M";
    }
    
    /// <summary>
    /// 30天从最低价涨幅项目
    /// </summary>
    public class PriceChangeFrom30DayLowItem
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 30天最低价
        /// </summary>
        public decimal Low30Day { get; set; }
        
        /// <summary>
        /// 30天最高价
        /// </summary>
        public decimal High30Day { get; set; }
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; }
        
        /// <summary>
        /// 涨幅百分比（相对30天最低价）
        /// </summary>
        public decimal GainPercent { get; set; }
        
        /// <summary>
        /// 跌幅百分比（相对30天最高价）
        /// </summary>
        public decimal FallFromHighPercent { get; set; }
        
        /// <summary>
        /// 涨幅显示
        /// </summary>
        public string GainPercentDisplay => $"+{GainPercent:F2}%";
        
        /// <summary>
        /// 跌幅显示
        /// </summary>
        public string FallFromHighPercentDisplay => $"{FallFromHighPercent:F2}%";
    }
    
    /// <summary>
    /// 30天从最高价跌幅项目
    /// </summary>
    public class PriceChangeFrom30DayHighItem
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 30天最低价
        /// </summary>
        public decimal Low30Day { get; set; }
        
        /// <summary>
        /// 30天最高价
        /// </summary>
        public decimal High30Day { get; set; }
        
        /// <summary>
        /// 当前价格
        /// </summary>
        public decimal CurrentPrice { get; set; }
        
        /// <summary>
        /// 跌幅百分比（相对30天最高价）
        /// </summary>
        public decimal FallPercent { get; set; }
        
        /// <summary>
        /// 涨幅百分比（相对30天最低价）
        /// </summary>
        public decimal GainFromLowPercent { get; set; }
        
        /// <summary>
        /// 跌幅显示
        /// </summary>
        public string FallPercentDisplay => $"{FallPercent:F2}%";
        
        /// <summary>
        /// 涨幅显示
        /// </summary>
        public string GainFromLowPercentDisplay => $"+{GainFromLowPercent:F2}%";
    }
} 