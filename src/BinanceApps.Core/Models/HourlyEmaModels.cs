using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 小时均线监控参数
    /// </summary>
    public class HourlyEmaParameters
    {
        /// <summary>
        /// N天均线（用于计算过去N个K线的EMA）
        /// </summary>
        public int EmaPeriod { get; set; } = 26;

        /// <summary>
        /// X根K线（获取最近X根1小时K线）
        /// </summary>
        public int KlineCount { get; set; } = 100;
    }

    /// <summary>
    /// 单个合约的小时K线数据
    /// </summary>
    public class HourlyKlineData
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// K线数据列表
        /// </summary>
        public List<Kline> Klines { get; set; } = new List<Kline>();

        /// <summary>
        /// EMA均线数据（时间 -> EMA值）
        /// </summary>
        public Dictionary<DateTime, decimal> EmaValues { get; set; } = new Dictionary<DateTime, decimal>();

        /// <summary>
        /// 连续大于EMA的K线数量
        /// </summary>
        public int AboveEmaCount { get; set; }

        /// <summary>
        /// 连续小于EMA的K线数量
        /// </summary>
        public int BelowEmaCount { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 小时均线监控结果
    /// </summary>
    public class HourlyEmaMonitorResult
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 最新价格（最后K线的收盘价，与连续数量计算保持一致）
        /// </summary>
        public decimal LastPrice { get; set; }

        /// <summary>
        /// 当前EMA值
        /// </summary>
        public decimal CurrentEma { get; set; }

        /// <summary>
        /// 距离EMA的百分比
        /// </summary>
        public decimal DistancePercent { get; set; }

        /// <summary>
        /// 24H涨幅
        /// </summary>
        public decimal PriceChangePercent { get; set; }

        /// <summary>
        /// K线数据数量
        /// </summary>
        public int KlineCount { get; set; }

        /// <summary>
        /// 连续大于EMA的K线数量
        /// </summary>
        public int AboveEmaCount { get; set; }

        /// <summary>
        /// 连续小于EMA的K线数量
        /// </summary>
        public int BelowEmaCount { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 24H成交额（USDT）
        /// </summary>
        public decimal QuoteVolume24h { get; set; }

        /// <summary>
        /// 24H成交额文本
        /// </summary>
        public string QuoteVolumeText { get; set; } = string.Empty;

        /// <summary>
        /// 流通量
        /// </summary>
        public decimal CirculatingSupply { get; set; }

        /// <summary>
        /// 发行总量
        /// </summary>
        public decimal TotalSupply { get; set; }

        /// <summary>
        /// 流通率（%）
        /// </summary>
        public decimal CirculationRate { get; set; }

        /// <summary>
        /// 量比
        /// </summary>
        public decimal VolumeRatio { get; set; }

        /// <summary>
        /// 合约简介
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 流通市值（USDT）
        /// </summary>
        public decimal CirculatingMarketCap { get; set; }

        /// <summary>
        /// 发行市值（USDT）
        /// </summary>
        public decimal TotalMarketCap { get; set; }
    }

    /// <summary>
    /// 筛选条件
    /// </summary>
    public class HourlyEmaFilter
    {
        /// <summary>
        /// 最小大于EMA数量
        /// </summary>
        public int? MinAboveEmaCount { get; set; }

        /// <summary>
        /// 最小小于EMA数量
        /// </summary>
        public int? MinBelowEmaCount { get; set; }

        /// <summary>
        /// 最小24H成交额（USDT）
        /// </summary>
        public decimal? MinQuoteVolume { get; set; }

        /// <summary>
        /// 最小量比（%）
        /// </summary>
        public decimal? MinVolumeRatio { get; set; }

        /// <summary>
        /// 最大流通率（%）
        /// </summary>
        public decimal? MaxCirculationRate { get; set; }

        /// <summary>
        /// 最大流通市值（USDT）
        /// </summary>
        public decimal? MaxCirculatingMarketCap { get; set; }

        /// <summary>
        /// 最大发行市值（USDT）
        /// </summary>
        public decimal? MaxTotalMarketCap { get; set; }
    }

    /// <summary>
    /// 小时均线监控配置（用于本地保存）
    /// </summary>
    public class HourlyEmaConfig
    {
        /// <summary>
        /// 参数配置
        /// </summary>
        public HourlyEmaParameters Parameters { get; set; } = new HourlyEmaParameters();
        
        /// <summary>
        /// 筛选条件
        /// </summary>
        public HourlyEmaFilter Filter { get; set; } = new HourlyEmaFilter();
        
        /// <summary>
        /// 最后保存时间
        /// </summary>
        public DateTime LastSaved { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 小时K线下载进度信息
    /// </summary>
    public class HourlyKlineDownloadProgress
    {
        /// <summary>
        /// 总合约数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 已完成数
        /// </summary>
        public int CompletedCount { get; set; }

        /// <summary>
        /// 当前正在处理的合约
        /// </summary>
        public string CurrentSymbol { get; set; } = string.Empty;

        /// <summary>
        /// 进度百分比
        /// </summary>
        public int ProgressPercent => TotalCount > 0 ? (CompletedCount * 100 / TotalCount) : 0;

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsCompleted => CompletedCount >= TotalCount;
    }
}

