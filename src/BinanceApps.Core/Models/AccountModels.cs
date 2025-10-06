namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 账户余额
    /// </summary>
    public class Balance
    {
        /// <summary>
        /// 资产名称
        /// </summary>
        public string Asset { get; set; } = string.Empty;

        /// <summary>
        /// 可用余额
        /// </summary>
        public decimal AvailableBalance { get; set; }

        /// <summary>
        /// 总余额
        /// </summary>
        public decimal TotalBalance { get; set; }

        /// <summary>
        /// 冻结余额
        /// </summary>
        public decimal FrozenBalance { get; set; }

        /// <summary>
        /// 钱包余额
        /// </summary>
        public decimal WalletBalance { get; set; }

        /// <summary>
        /// 未实现盈亏
        /// </summary>
        public decimal UnrealizedPnl { get; set; }

        /// <summary>
        /// 保证金余额
        /// </summary>
        public decimal MarginBalance { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>
    /// 账户信息
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// 账户类型
        /// </summary>
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// 是否可交易
        /// </summary>
        public bool CanTrade { get; set; }

        /// <summary>
        /// 是否可提现
        /// </summary>
        public bool CanWithdraw { get; set; }

        /// <summary>
        /// 是否可充值
        /// </summary>
        public bool CanDeposit { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// 总资产（USDT）
        /// </summary>
        public decimal TotalWalletBalance { get; set; }

        /// <summary>
        /// 总未实现盈亏（USDT）
        /// </summary>
        public decimal TotalUnrealizedPnl { get; set; }

        /// <summary>
        /// 总保证金余额（USDT）
        /// </summary>
        public decimal TotalMarginBalance { get; set; }

        /// <summary>
        /// 可用余额（USDT）
        /// </summary>
        public decimal TotalAvailableBalance { get; set; }

        /// <summary>
        /// 账户余额列表
        /// </summary>
        public List<Balance> Balances { get; set; } = new List<Balance>();
    }

    /// <summary>
    /// 交易历史记录
    /// </summary>
    public class TradeHistory
    {
        /// <summary>
        /// 交易ID
        /// </summary>
        public long TradeId { get; set; }

        /// <summary>
        /// 订单ID
        /// </summary>
        public long OrderId { get; set; }

        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 价格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 成交金额
        /// </summary>
        public decimal QuoteQuantity { get; set; }

        /// <summary>
        /// 手续费
        /// </summary>
        public decimal Commission { get; set; }

        /// <summary>
        /// 手续费资产
        /// </summary>
        public string CommissionAsset { get; set; } = string.Empty;

        /// <summary>
        /// 交易时间
        /// </summary>
        public DateTime TradeTime { get; set; }

        /// <summary>
        /// 是否买方
        /// </summary>
        public bool IsBuyer { get; set; }

        /// <summary>
        /// 是否做市商
        /// </summary>
        public bool IsMaker { get; set; }
    }

    /// <summary>
    /// 资金费率
    /// </summary>
    public class FundingRate
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 资金费率
        /// </summary>
        public decimal FundingRateValue { get; set; }

        /// <summary>
        /// 资金费率时间
        /// </summary>
        public DateTime FundingTime { get; set; }

        /// <summary>
        /// 下次资金费率时间
        /// </summary>
        public DateTime NextFundingTime { get; set; }

        /// <summary>
        /// 预测资金费率
        /// </summary>
        public decimal? PredictedFundingRate { get; set; }
    }

    /// <summary>
    /// 杠杆信息
    /// </summary>
    public class LeverageInfo
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 杠杆倍数
        /// </summary>
        public int Leverage { get; set; }

        /// <summary>
        /// 最大杠杆倍数
        /// </summary>
        public int MaxLeverage { get; set; }

        /// <summary>
        /// 保证金类型
        /// </summary>
        public string MarginType { get; set; } = string.Empty;

        /// <summary>
        /// 是否可调整杠杆
        /// </summary>
        public bool Adjustable { get; set; }
    }

    /// <summary>
    /// 风险信息
    /// </summary>
    public class RiskInfo
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 保证金率
        /// </summary>
        public decimal MarginRatio { get; set; }

        /// <summary>
        /// 维持保证金率
        /// </summary>
        public decimal MaintMarginRatio { get; set; }

        /// <summary>
        /// 强平价格
        /// </summary>
        public decimal LiquidationPrice { get; set; }

        /// <summary>
        /// 风险等级
        /// </summary>
        public string RiskLevel { get; set; } = string.Empty;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>
    /// 持仓信息
    /// </summary>
    public class Position
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 持仓方向
        /// </summary>
        public PositionSide PositionSide { get; set; }

        /// <summary>
        /// 持仓数量
        /// </summary>
        public decimal PositionAmt { get; set; }

        /// <summary>
        /// 持仓价值
        /// </summary>
        public decimal PositionValue { get; set; }

        /// <summary>
        /// 开仓价格
        /// </summary>
        public decimal EntryPrice { get; set; }

        /// <summary>
        /// 标记价格
        /// </summary>
        public decimal MarkPrice { get; set; }

        /// <summary>
        /// 未实现盈亏
        /// </summary>
        public decimal UnrealizedPnl { get; set; }

        /// <summary>
        /// 已实现盈亏
        /// </summary>
        public decimal RealizedPnl { get; set; }

        /// <summary>
        /// 杠杆倍数
        /// </summary>
        public int Leverage { get; set; }

        /// <summary>
        /// 保证金类型
        /// </summary>
        public string MarginType { get; set; } = string.Empty;

        /// <summary>
        /// 是否自动追加保证金
        /// </summary>
        public bool AutoAddMargin { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }
} 