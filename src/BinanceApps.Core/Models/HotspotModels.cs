using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 热点追踪配置
    /// </summary>
    public class HotspotTrackingConfig
    {
        /// <summary>
        /// 量比阈值（百分比）
        /// </summary>
        public decimal VolumeRatioThreshold { get; set; } = 100m;
        
        /// <summary>
        /// N天高点天数
        /// </summary>
        public int HighPriceDays { get; set; } = 20;
        
        /// <summary>
        /// 回调一区阈值（百分比）
        /// </summary>
        public decimal PullbackZone1Threshold { get; set; } = 10m;
        
        /// <summary>
        /// 回调二区阈值（百分比）
        /// </summary>
        public decimal PullbackZone2Threshold { get; set; } = 20m;
        
        /// <summary>
        /// 自动检索间隔（秒）
        /// </summary>
        public int ScanIntervalSeconds { get; set; } = 5;
        
        /// <summary>
        /// 缓存倒计时（小时）
        /// </summary>
        public int CacheExpiryHours { get; set; } = 240; // 10天
        
        /// <summary>
        /// 最小流通市值（万）
        /// </summary>
        public decimal MinCirculatingMarketCap { get; set; } = 0m;
        
        /// <summary>
        /// 最大流通市值（万）
        /// </summary>
        public decimal MaxCirculatingMarketCap { get; set; } = 5000m;
    }
    
    /// <summary>
    /// 热点合约数据
    /// </summary>
    public class HotspotContract
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
        /// 24H涨幅
        /// </summary>
        public decimal PriceChangePercent24h { get; set; }
        
        /// <summary>
        /// 24H成交额
        /// </summary>
        public decimal QuoteVolume24h { get; set; }
        
        /// <summary>
        /// 量比（百分比）
        /// </summary>
        public decimal VolumeRatio { get; set; }
        
        /// <summary>
        /// N天最高价
        /// </summary>
        public decimal HighPriceNDays { get; set; }
        
        /// <summary>
        /// 最新价相对N天最高价的涨幅（百分比）
        /// </summary>
        public decimal PriceChangeFromNDayHigh { get; set; }
        
        /// <summary>
        /// 流通市值
        /// </summary>
        public decimal CirculatingMarketCap { get; set; }
        
        /// <summary>
        /// 发行总市值
        /// </summary>
        public decimal TotalMarketCap { get; set; }
        
        /// <summary>
        /// 流通率（百分比）
        /// </summary>
        public decimal CirculatingRate { get; set; }
    }
    
    /// <summary>
    /// 缓存区合约数据
    /// </summary>
    public class CachedHotspotContract : HotspotContract
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
        /// 录入时的N天最高价（用于计算录入时相对前高的涨幅）
        /// </summary>
        public decimal EntryNDayHighPrice { get; set; }
        
        /// <summary>
        /// 录入后最高价
        /// </summary>
        public decimal HighestPriceAfterEntry { get; set; }
        
        /// <summary>
        /// 缓存到期时间
        /// </summary>
        public DateTime ExpiryTime { get; set; }
        
        /// <summary>
        /// 倒计时开始时间（最后一次重置倒计时的时间）
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
        /// 录入时相对前高涨幅（录入价相对于N天最高价的涨幅，百分比）
        /// </summary>
        public decimal EntryPriceGainFromNDayHigh
        {
            get
            {
                if (EntryNDayHighPrice <= 0)
                    return 0;
                return ((EntryPrice - EntryNDayHighPrice) / EntryNDayHighPrice) * 100m;
            }
        }
        
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
    /// 回收区合约数据
    /// </summary>
    public class RecycledHotspotContract : CachedHotspotContract
    {
        /// <summary>
        /// 回收时间
        /// </summary>
        public DateTime RecycleTime { get; set; }
        
        /// <summary>
        /// 回收到期时间（3天后）
        /// </summary>
        public DateTime RecycleExpiryTime { get; set; }
        
        /// <summary>
        /// 是否应该删除
        /// </summary>
        public bool ShouldDelete => DateTime.Now >= RecycleExpiryTime;
    }
    
    /// <summary>
    /// 热点追踪持久化数据
    /// </summary>
    public class HotspotTrackingData
    {
        /// <summary>
        /// 配置
        /// </summary>
        public HotspotTrackingConfig Config { get; set; } = new();
        
        /// <summary>
        /// 缓存区合约
        /// </summary>
        public Dictionary<string, CachedHotspotContract> CachedContracts { get; set; } = new();
        
        /// <summary>
        /// 回收区合约
        /// </summary>
        public Dictionary<string, RecycledHotspotContract> RecycledContracts { get; set; } = new();
        
        /// <summary>
        /// 最后保存时间
        /// </summary>
        public DateTime LastSaveTime { get; set; } = DateTime.Now;
    }
}

