namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 订单类型
    /// </summary>
    public enum OrderType
    {
        /// <summary>
        /// 市价单
        /// </summary>
        Market,
        
        /// <summary>
        /// 限价单
        /// </summary>
        Limit,
        
        /// <summary>
        /// 止损单
        /// </summary>
        Stop,
        
        /// <summary>
        /// 止损限价单
        /// </summary>
        StopLimit,
        
        /// <summary>
        /// 跟踪止损单
        /// </summary>
        TrailingStop,
        
        /// <summary>
        /// 冰山单
        /// </summary>
        Iceberg
    }

    /// <summary>
    /// 订单方向
    /// </summary>
    public enum OrderSide
    {
        /// <summary>
        /// 买入
        /// </summary>
        Buy,
        
        /// <summary>
        /// 卖出
        /// </summary>
        Sell
    }

    /// <summary>
    /// 持仓方向
    /// </summary>
    public enum PositionSide
    {
        /// <summary>
        /// 多头
        /// </summary>
        Long,
        
        /// <summary>
        /// 空头
        /// </summary>
        Short,
        
        /// <summary>
        /// 双向持仓
        /// </summary>
        Both
    }

    /// <summary>
    /// 订单状态
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>
        /// 新建
        /// </summary>
        New,
        
        /// <summary>
        /// 部分成交
        /// </summary>
        PartiallyFilled,
        
        /// <summary>
        /// 全部成交
        /// </summary>
        Filled,
        
        /// <summary>
        /// 已取消
        /// </summary>
        Canceled,
        
        /// <summary>
        /// 已拒绝
        /// </summary>
        Rejected,
        
        /// <summary>
        /// 已过期
        /// </summary>
        Expired
    }

    /// <summary>
    /// 时间类型
    /// </summary>
    public enum TimeInForce
    {
        /// <summary>
        /// 立即成交或取消
        /// </summary>
        IOC,
        
        /// <summary>
        /// 全部成交或取消
        /// </summary>
        FOK,
        
        /// <summary>
        /// 一直有效直到取消
        /// </summary>
        GTC,
        
        /// <summary>
        /// 当日有效
        /// </summary>
        DAY
    }

    /// <summary>
    /// 合约类型
    /// </summary>
    public enum ContractType
    {
        /// <summary>
        /// 永续合约
        /// </summary>
        Perpetual,
        
        /// <summary>
        /// 季度合约
        /// </summary>
        Quarterly,
        
        /// <summary>
        /// 次季度合约
        /// </summary>
        NextQuarterly
    }

    /// <summary>
    /// K线时间间隔
    /// </summary>
    public enum KlineInterval
    {
        /// <summary>
        /// 1分钟
        /// </summary>
        OneMinute,
        
        /// <summary>
        /// 3分钟
        /// </summary>
        ThreeMinutes,
        
        /// <summary>
        /// 5分钟
        /// </summary>
        FiveMinutes,
        
        /// <summary>
        /// 15分钟
        /// </summary>
        FifteenMinutes,
        
        /// <summary>
        /// 30分钟
        /// </summary>
        ThirtyMinutes,
        
        /// <summary>
        /// 1小时
        /// </summary>
        OneHour,
        
        /// <summary>
        /// 2小时
        /// </summary>
        TwoHours,
        
        /// <summary>
        /// 4小时
        /// </summary>
        FourHours,
        
        /// <summary>
        /// 6小时
        /// </summary>
        SixHours,
        
        /// <summary>
        /// 8小时
        /// </summary>
        EightHours,
        
        /// <summary>
        /// 12小时
        /// </summary>
        TwelveHours,
        
        /// <summary>
        /// 1天
        /// </summary>
        OneDay,
        
        /// <summary>
        /// 3天
        /// </summary>
        ThreeDays,
        
        /// <summary>
        /// 1周
        /// </summary>
        OneWeek,
        
        /// <summary>
        /// 1月
        /// </summary>
        OneMonth
    }
} 