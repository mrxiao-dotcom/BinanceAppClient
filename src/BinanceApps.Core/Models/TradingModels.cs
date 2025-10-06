using System.ComponentModel.DataAnnotations;

namespace BinanceApps.Core.Models
{
    /// <summary>
    /// 基础订单模型
    /// </summary>
    public class BaseOrder
    {
        /// <summary>
        /// 订单ID
        /// </summary>
        public long OrderId { get; set; }

        /// <summary>
        /// 客户端订单ID
        /// </summary>
        public string ClientOrderId { get; set; } = string.Empty;

        /// <summary>
        /// 交易对
        /// </summary>
        [Required]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 订单类型
        /// </summary>
        public OrderType OrderType { get; set; }

        /// <summary>
        /// 订单方向
        /// </summary>
        public OrderSide Side { get; set; }

        /// <summary>
        /// 持仓方向
        /// </summary>
        public PositionSide PositionSide { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        [Range(0.000001, double.MaxValue)]
        public decimal Quantity { get; set; }

        /// <summary>
        /// 价格
        /// </summary>
        [Range(0.000001, double.MaxValue)]
        public decimal Price { get; set; }

        /// <summary>
        /// 时间类型
        /// </summary>
        public TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// 订单状态
        /// </summary>
        public OrderStatus Status { get; set; }

        /// <summary>
        /// 已成交数量
        /// </summary>
        public decimal ExecutedQuantity { get; set; }

        /// <summary>
        /// 已成交金额
        /// </summary>
        public decimal ExecutedQuoteQuantity { get; set; }

        /// <summary>
        /// 手续费
        /// </summary>
        public decimal Commission { get; set; }

        /// <summary>
        /// 手续费资产
        /// </summary>
        public string CommissionAsset { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// 是否工作
        /// </summary>
        public bool IsWorking { get; set; }
    }

    /// <summary>
    /// 永续合约订单
    /// </summary>
    public class PerpetualOrder : BaseOrder
    {
        /// <summary>
        /// 杠杆倍数
        /// </summary>
        [Range(1, 125)]
        public int Leverage { get; set; }

        /// <summary>
        /// 保证金类型
        /// </summary>
        public string MarginType { get; set; } = "isolated";

        /// <summary>
        /// 激活价格（止损单）
        /// </summary>
        public decimal? ActivatePrice { get; set; }

        /// <summary>
        /// 回调率（跟踪止损单）
        /// </summary>
        public decimal? CallbackRate { get; set; }

        /// <summary>
        /// 工作类型
        /// </summary>
        public string WorkingType { get; set; } = "CONTRACT_PRICE";

        /// <summary>
        /// 价格保护
        /// </summary>
        public bool PriceProtect { get; set; }

        /// <summary>
        /// 减少数量
        /// </summary>
        public bool ReduceOnly { get; set; }

        /// <summary>
        /// 关闭位置
        /// </summary>
        public bool ClosePosition { get; set; }
    }

    /// <summary>
    /// 条件单
    /// </summary>
    public class ConditionalOrder : BaseOrder
    {
        /// <summary>
        /// 条件单ID
        /// </summary>
        public long ConditionalOrderId { get; set; }

        /// <summary>
        /// 触发价格
        /// </summary>
        [Required]
        public decimal TriggerPrice { get; set; }

        /// <summary>
        /// 触发类型
        /// </summary>
        public string TriggerType { get; set; } = "CONTRACT_PRICE";

        /// <summary>
        /// 条件单状态
        /// </summary>
        public string ConditionalOrderStatus { get; set; } = string.Empty;

        /// <summary>
        /// 条件单类型
        /// </summary>
        public string ConditionalOrderType { get; set; } = string.Empty;

        /// <summary>
        /// 条件单价格
        /// </summary>
        public decimal ConditionalOrderPrice { get; set; }

        /// <summary>
        /// 条件单数量
        /// </summary>
        public decimal ConditionalOrderQuantity { get; set; }
    }



    /// <summary>
    /// 交易对信息
    /// </summary>
    public class SymbolInfo
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 基础资产
        /// </summary>
        public string BaseAsset { get; set; } = string.Empty;

        /// <summary>
        /// 计价资产
        /// </summary>
        public string QuoteAsset { get; set; } = string.Empty;

        /// <summary>
        /// 最小数量
        /// </summary>
        public decimal MinQty { get; set; }

        /// <summary>
        /// 最大数量
        /// </summary>
        public decimal MaxQty { get; set; }

        /// <summary>
        /// 数量精度
        /// </summary>
        public int QtyPrecision { get; set; }

        /// <summary>
        /// 价格精度
        /// </summary>
        public int PricePrecision { get; set; }

        /// <summary>
        /// 最小价格
        /// </summary>
        public decimal MinPrice { get; set; }

        /// <summary>
        /// 最大价格
        /// </summary>
        public decimal MaxPrice { get; set; }

        /// <summary>
        /// 最小名义价值
        /// </summary>
        public decimal MinNotional { get; set; }

        /// <summary>
        /// 是否可交易
        /// </summary>
        public bool IsTrading { get; set; }

        /// <summary>
        /// 合约类型
        /// </summary>
        public ContractType ContractType { get; set; }

        /// <summary>
        /// 合约到期时间
        /// </summary>
        public DateTime? ExpiryDate { get; set; }
    }

    /// <summary>
    /// 下单请求
    /// </summary>
    public class PlaceOrderRequest
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 订单方向
        /// </summary>
        public OrderSide Side { get; set; }

        /// <summary>
        /// 订单类型
        /// </summary>
        public OrderType OrderType { get; set; }

        /// <summary>
        /// 持仓方向
        /// </summary>
        public PositionSide PositionSide { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 价格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 时间类型
        /// </summary>
        public TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// 客户端订单ID
        /// </summary>
        public string ClientOrderId { get; set; } = string.Empty;

        /// <summary>
        /// 减少数量
        /// </summary>
        public bool ReduceOnly { get; set; }

        /// <summary>
        /// 是否关闭持仓
        /// </summary>
        public bool ClosePosition { get; set; }
    }

    /// <summary>
    /// 下单结果
    /// </summary>
    public class OrderResult
    {
        /// <summary>
        /// 订单ID
        /// </summary>
        public long OrderId { get; set; }

        /// <summary>
        /// 客户端订单ID
        /// </summary>
        public string ClientOrderId { get; set; } = string.Empty;

        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 订单状态
        /// </summary>
        public OrderStatus Status { get; set; }

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }
    }

    /// <summary>
    /// 取消订单结果
    /// </summary>
    public class CancelOrderResult
    {
        /// <summary>
        /// 订单ID
        /// </summary>
        public long OrderId { get; set; }

        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 取消时间
        /// </summary>
        public DateTime CancelTime { get; set; }
    }
} 