using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 涨幅榜追踪配置
    /// </summary>
    public class GainerTrackingConfig
    {
        /// <summary>
        /// N天涨幅（默认30天）
        /// </summary>
        public int NDays { get; set; } = 30;
        
        /// <summary>
        /// 排行数（默认前30）
        /// </summary>
        public int TopCount { get; set; } = 30;
        
        /// <summary>
        /// 定时扫描间隔（秒，默认5秒）
        /// </summary>
        public int ScanIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// 回撤一区比例（默认10%）
        /// </summary>
        public decimal PullbackZone1Threshold { get; set; } = 10m;
        
        /// <summary>
        /// 回撤二区比例（默认20%）
        /// </summary>
        public decimal PullbackZone2Threshold { get; set; } = 20m;
        
        /// <summary>
        /// 缓存时间（小时，默认240小时）
        /// </summary>
        public int CacheExpiryHours { get; set; } = 240;
    }
    
    /// <summary>
    /// 涨幅榜合约数据
    /// </summary>
    public class GainerContract
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        
        /// <summary>
        /// 最新价
        /// </summary>
        public decimal LastPrice { get; set; }
        
        /// <summary>
        /// N天内最低价
        /// </summary>
        public decimal NDayAgoPrice { get; set; }
        
        /// <summary>
        /// N天涨幅（百分比）
        /// </summary>
        public decimal NDayGainPercent { get; set; }
        
        /// <summary>
        /// 24H涨幅
        /// </summary>
        public decimal PriceChangePercent24h { get; set; }
        
        /// <summary>
        /// 24H成交额
        /// </summary>
        public decimal QuoteVolume24h { get; set; }
        
        /// <summary>
        /// 流通市值
        /// </summary>
        public decimal CirculatingMarketCap { get; set; }
        
        /// <summary>
        /// 24H量比（成交额/流通市值的百分比）
        /// </summary>
        public decimal VolumeRatio
        {
            get
            {
                if (CirculatingMarketCap <= 0)
                    return 0;
                return (QuoteVolume24h / CirculatingMarketCap) * 100m;
            }
        }
        
        /// <summary>
        /// 排名
        /// </summary>
        public int Rank { get; set; }
    }
    
    /// <summary>
    /// 缓存区涨幅榜合约数据
    /// </summary>
    public class CachedGainerContract : GainerContract
    {
        /// <summary>
        /// 录入时间
        /// </summary>
        public DateTime EntryTime { get; set; }
        
        /// <summary>
        /// 录入时价格
        /// </summary>
        public decimal EntryPrice { get; set; }
        
        /// <summary>
        /// 录入时排名
        /// </summary>
        public int EntryRank { get; set; }
        
        /// <summary>
        /// 录入后最高价
        /// </summary>
        public decimal HighestPriceAfterEntry { get; set; }
        
        /// <summary>
        /// 缓存到期时间
        /// </summary>
        public DateTime ExpiryTime { get; set; }
        
        /// <summary>
        /// 倒计时开始时间
        /// </summary>
        public DateTime CountdownStartTime { get; set; }
        
        /// <summary>
        /// 剩余缓存时间（小时）
        /// </summary>
        public double RemainingCacheHours => (ExpiryTime - DateTime.Now).TotalHours;
        
        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired => DateTime.Now >= ExpiryTime;
        
        /// <summary>
        /// 存在缓存区时间（小时）
        /// </summary>
        public double CachedDurationHours => (DateTime.Now - EntryTime).TotalHours;
        
        /// <summary>
        /// 录入后涨幅（最新价相对于录入价的涨幅，百分比）
        /// </summary>
        public decimal PriceGainFromEntry
        {
            get
            {
                if (EntryPrice <= 0)
                    return 0;
                return ((LastPrice - EntryPrice) / EntryPrice) * 100m;
            }
        }
        
        /// <summary>
        /// 当前回撤幅度（相对于录入后最高价，百分比）
        /// </summary>
        public decimal CurrentPullbackPercent
        {
            get
            {
                if (HighestPriceAfterEntry <= 0)
                    return 0;
                return ((HighestPriceAfterEntry - LastPrice) / HighestPriceAfterEntry) * 100m;
            }
        }
    }
    
    /// <summary>
    /// 回收区涨幅榜合约数据
    /// </summary>
    public class RecycledGainerContract : GainerContract
    {
        /// <summary>
        /// 回收时间
        /// </summary>
        public DateTime RecycleTime { get; set; }
        
        /// <summary>
        /// 缓存时长（小时）
        /// </summary>
        public double CachedDurationHours { get; set; }
    }
    
    /// <summary>
    /// 涨幅榜追踪数据（用于持久化）
    /// </summary>
    public class GainerTrackingData
    {
        /// <summary>
        /// 配置
        /// </summary>
        public GainerTrackingConfig Config { get; set; } = new();
        
        /// <summary>
        /// 缓存区合约
        /// </summary>
        public Dictionary<string, CachedGainerContract> CachedContracts { get; set; } = new();
        
        /// <summary>
        /// 回收区合约
        /// </summary>
        public Dictionary<string, RecycledGainerContract> RecycledContracts { get; set; } = new();
    }
}

