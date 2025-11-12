using System;
using System.Collections.Generic;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 监控类型
    /// </summary>
    public enum MonitorType
    {
        /// <summary>
        /// 多头监控
        /// </summary>
        Long,
        
        /// <summary>
        /// 空头监控
        /// </summary>
        Short
    }

    /// <summary>
    /// 监控项目
    /// </summary>
    public class MonitorItem
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 监控类型
        /// </summary>
        public MonitorType Type { get; set; }

        /// <summary>
        /// 加入时的价格
        /// </summary>
        public decimal EntryPrice { get; set; }

        /// <summary>
        /// 加入时间
        /// </summary>
        public DateTime EntryTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 最新价格
        /// </summary>
        public decimal LastPrice { get; set; }

        /// <summary>
        /// 当前EMA
        /// </summary>
        public decimal CurrentEma { get; set; }

        /// <summary>
        /// 距离EMA百分比
        /// </summary>
        public decimal DistancePercent { get; set; }

        /// <summary>
        /// 是否已预警
        /// </summary>
        public bool IsAlerted { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 预警记录
    /// </summary>
    public class MonitorAlert
    {
        /// <summary>
        /// 合约名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 监控类型
        /// </summary>
        public MonitorType Type { get; set; }

        /// <summary>
        /// 监控类型文本
        /// </summary>
        public string TypeText => Type == MonitorType.Long ? "多头" : "空头";

        /// <summary>
        /// 选入时价格
        /// </summary>
        public decimal EntryPrice { get; set; }

        /// <summary>
        /// 预警时价格
        /// </summary>
        public decimal AlertPrice { get; set; }

        /// <summary>
        /// 当前EMA
        /// </summary>
        public decimal CurrentEma { get; set; }

        /// <summary>
        /// 距离EMA百分比
        /// </summary>
        public decimal DistancePercent { get; set; }

        /// <summary>
        /// 预警时间
        /// </summary>
        public DateTime AlertTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 预警时间文本
        /// </summary>
        public string AlertTimeText => AlertTime.ToString("MM-dd HH:mm:ss");
    }

    /// <summary>
    /// 监控配置
    /// </summary>
    public class FloatingMonitorConfig
    {
        /// <summary>
        /// EMA周期
        /// </summary>
        public int EmaPeriod { get; set; } = 26;

        /// <summary>
        /// K线数量
        /// </summary>
        public int KlineCount { get; set; } = 100;

        /// <summary>
        /// 多头预警范围（%）
        /// </summary>
        public decimal LongAlertRange { get; set; } = 10;

        /// <summary>
        /// 空头预警范围（%）
        /// </summary>
        public decimal ShortAlertRange { get; set; } = 10;

        /// <summary>
        /// 监控间隔（分钟）
        /// </summary>
        public int MonitorIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// 多头监控列表
        /// </summary>
        public List<MonitorItem> LongMonitors { get; set; } = new List<MonitorItem>();

        /// <summary>
        /// 空头监控列表
        /// </summary>
        public List<MonitorItem> ShortMonitors { get; set; } = new List<MonitorItem>();
    }
}

