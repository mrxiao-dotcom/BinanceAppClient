using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 跌幅榜追踪配置
    /// </summary>
    public class LoserTrackingConfig
    {
        /// <summary>
        /// N天跌幅（默认30天）
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
        /// 反弹一区比例（默认10%）
        /// </summary>
        public decimal ReboundZone1Threshold { get; set; } = 10m;
        
        /// <summary>
        /// 反弹二区比例（默认20%）
        /// </summary>
        public decimal ReboundZone2Threshold { get; set; } = 20m;
        
        /// <summary>
        /// 缓存时间（小时，默认240小时）
        /// </summary>
        public int CacheExpiryHours { get; set; } = 240;
    }
    
    /// <summary>
    /// 跌幅榜合约数据
    /// </summary>
    public class LoserContract
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
        /// N天内最高价
        /// </summary>
        public decimal NDayHighPrice { get; set; }
        
        /// <summary>
        /// N天跌幅（百分比，负值）
        /// </summary>
        public decimal NDayLossPercent { get; set; }
        
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
    /// 缓存区跌幅榜合约数据
    /// </summary>
    public class CachedLoserContract : LoserContract
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
        /// 录入后最低价
        /// </summary>
        public decimal LowestPriceAfterEntry { get; set; }
        
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
        /// 录入后跌幅（最新价相对于录入价的跌幅，百分比）
        /// </summary>
        public decimal PriceLossFromEntry
        {
            get
            {
                if (EntryPrice <= 0)
                    return 0;
                return ((LastPrice - EntryPrice) / EntryPrice) * 100m;
            }
        }
        
        /// <summary>
        /// 当前反弹幅度（相对于录入后最低价，百分比）
        /// </summary>
        public decimal CurrentReboundPercent
        {
            get
            {
                if (LowestPriceAfterEntry <= 0)
                    return 0;
                return ((LastPrice - LowestPriceAfterEntry) / LowestPriceAfterEntry) * 100m;
            }
        }
    }
    
    /// <summary>
    /// 回收区跌幅榜合约数据
    /// </summary>
    public class RecycledLoserContract : LoserContract
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
    /// 跌幅榜追踪数据（用于持久化）
    /// </summary>
    public class LoserTrackingData
    {
        /// <summary>
        /// 配置
        /// </summary>
        public LoserTrackingConfig Config { get; set; } = new();
        
        /// <summary>
        /// 缓存区合约
        /// </summary>
        public Dictionary<string, CachedLoserContract> CachedContracts { get; set; } = new();
        
        /// <summary>
        /// 回收区合约
        /// </summary>
        public Dictionary<string, RecycledLoserContract> RecycledContracts { get; set; } = new();
    }
}

