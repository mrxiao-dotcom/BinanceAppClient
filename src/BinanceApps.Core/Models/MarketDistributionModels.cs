using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 涨幅档位枚举
    /// </summary>
    public enum PriceChangeRange
    {
        Below_50,      // < -50%
        Minus_49_40,   // -49% ~ -40%
        Minus_39_30,   // -39% ~ -30%
        Minus_29_20,   // -29% ~ -20%
        Minus_19_10,   // -19% ~ -10%
        Minus_9_0,     // -9% ~ 0%
        Plus_0_10,     // 0% ~ 10%
        Plus_11_20,    // 11% ~ 20%
        Plus_21_30,    // 21% ~ 30%
        Plus_31_40,    // 31% ~ 40%
        Plus_41_50,    // 41% ~ 50%
        Above_50       // > 50%
    }

    /// <summary>
    /// 每日涨幅分布数据
    /// </summary>
    public class DailyPriceChangeDistribution
    {
        /// <summary>
        /// 日期
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// 是否是今天（24H实时数据）
        /// </summary>
        public bool IsToday { get; set; }

        /// <summary>
        /// 各档位的合约数量
        /// </summary>
        public Dictionary<PriceChangeRange, int> RangeCounts { get; set; } = new();

        /// <summary>
        /// 总合约数
        /// </summary>
        public int TotalSymbols { get; set; }

        /// <summary>
        /// 获取档位名称
        /// </summary>
        public static string GetRangeName(PriceChangeRange range)
        {
            return range switch
            {
                PriceChangeRange.Below_50 => "<-50%",
                PriceChangeRange.Minus_49_40 => "-49~-40%",
                PriceChangeRange.Minus_39_30 => "-39~-30%",
                PriceChangeRange.Minus_29_20 => "-29~-20%",
                PriceChangeRange.Minus_19_10 => "-19~-10%",
                PriceChangeRange.Minus_9_0 => "-9~0%",
                PriceChangeRange.Plus_0_10 => "0~10%",
                PriceChangeRange.Plus_11_20 => "11~20%",
                PriceChangeRange.Plus_21_30 => "21~30%",
                PriceChangeRange.Plus_31_40 => "31~40%",
                PriceChangeRange.Plus_41_50 => "41~50%",
                PriceChangeRange.Above_50 => ">50%",
                _ => "未知"
            };
        }

        /// <summary>
        /// 根据涨跌幅百分比判断所属档位
        /// </summary>
        public static PriceChangeRange GetRange(decimal priceChangePercent)
        {
            if (priceChangePercent < -50m) return PriceChangeRange.Below_50;
            if (priceChangePercent < -40m) return PriceChangeRange.Minus_49_40;
            if (priceChangePercent < -30m) return PriceChangeRange.Minus_39_30;
            if (priceChangePercent < -20m) return PriceChangeRange.Minus_29_20;
            if (priceChangePercent < -10m) return PriceChangeRange.Minus_19_10;
            if (priceChangePercent < 0m) return PriceChangeRange.Minus_9_0;
            if (priceChangePercent < 10m) return PriceChangeRange.Plus_0_10;
            if (priceChangePercent < 20m) return PriceChangeRange.Plus_11_20;
            if (priceChangePercent < 30m) return PriceChangeRange.Plus_21_30;
            if (priceChangePercent < 40m) return PriceChangeRange.Plus_31_40;
            if (priceChangePercent < 50m) return PriceChangeRange.Plus_41_50;
            return PriceChangeRange.Above_50;
        }

        /// <summary>
        /// 获取所有档位的有序列表
        /// </summary>
        public static List<PriceChangeRange> GetAllRanges()
        {
            return new List<PriceChangeRange>
            {
                PriceChangeRange.Below_50,
                PriceChangeRange.Minus_49_40,
                PriceChangeRange.Minus_39_30,
                PriceChangeRange.Minus_29_20,
                PriceChangeRange.Minus_19_10,
                PriceChangeRange.Minus_9_0,
                PriceChangeRange.Plus_0_10,
                PriceChangeRange.Plus_11_20,
                PriceChangeRange.Plus_21_30,
                PriceChangeRange.Plus_31_40,
                PriceChangeRange.Plus_41_50,
                PriceChangeRange.Above_50
            };
        }
    }

    /// <summary>
    /// 市场涨幅分布分析结果（包含多天数据）
    /// </summary>
    public class MarketDistributionAnalysisResult
    {
        /// <summary>
        /// 多天的分布数据（按日期倒序，最新的在前）
        /// </summary>
        public List<DailyPriceChangeDistribution> DailyDistributions { get; set; } = new();

        /// <summary>
        /// 分析的天数
        /// </summary>
        public int Days { get; set; } = 5;
    }
}

