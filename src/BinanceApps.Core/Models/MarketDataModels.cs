namespace BinanceApps.Core.Models
{
    /// <summary>
    /// K线数据
    /// </summary>
    public class KlineData
    {
        /// <summary>
        /// 开盘时间
        /// </summary>
        public DateTime OpenTime { get; set; }

        /// <summary>
        /// 开盘价
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// 最高价
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// 最低价
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// 收盘价
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// 成交量
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// 收盘时间
        /// </summary>
        public DateTime CloseTime { get; set; }

        /// <summary>
        /// 成交额
        /// </summary>
        public decimal QuoteVolume { get; set; }

        /// <summary>
        /// 成交笔数
        /// </summary>
        public long TradeCount { get; set; }

        /// <summary>
        /// 主动买入成交量
        /// </summary>
        public decimal TakerBuyVolume { get; set; }

        /// <summary>
        /// 主动买入成交额
        /// </summary>
        public decimal TakerBuyQuoteVolume { get; set; }
    }

    /// <summary>
    /// 24小时价格统计
    /// </summary>
    public class PriceStatistics
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 价格变化
        /// </summary>
        public decimal PriceChange { get; set; }

        /// <summary>
        /// 价格变化百分比
        /// </summary>
        public decimal PriceChangePercent { get; set; }

        /// <summary>
        /// 加权平均价格
        /// </summary>
        public decimal WeightedAvgPrice { get; set; }

        /// <summary>
        /// 前一日收盘价
        /// </summary>
        public decimal PrevClosePrice { get; set; }

        /// <summary>
        /// 最新价格
        /// </summary>
        public decimal LastPrice { get; set; }

        /// <summary>
        /// 最新价格数量
        /// </summary>
        public decimal LastQty { get; set; }

        /// <summary>
        /// 最高价
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// 最低价
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// 成交量
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// 成交额
        /// </summary>
        public decimal QuoteVolume { get; set; }

        /// <summary>
        /// 开盘价
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// 开盘时间
        /// </summary>
        public DateTime OpenTime { get; set; }

        /// <summary>
        /// 收盘时间
        /// </summary>
        public DateTime CloseTime { get; set; }

        /// <summary>
        /// 第一笔交易ID
        /// </summary>
        public long FirstId { get; set; }

        /// <summary>
        /// 最后一笔交易ID
        /// </summary>
        public long LastId { get; set; }

        /// <summary>
        /// 成交笔数
        /// </summary>
        public long Count { get; set; }
    }

    /// <summary>
    /// 深度数据
    /// </summary>
    public class DepthData
    {
        /// <summary>
        /// 最后更新ID
        /// </summary>
        public long LastUpdateId { get; set; }

        /// <summary>
        /// 买单深度
        /// </summary>
        public List<DepthLevel> Bids { get; set; } = new List<DepthLevel>();

        /// <summary>
        /// 卖单深度
        /// </summary>
        public List<DepthLevel> Asks { get; set; } = new List<DepthLevel>();

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }

    /// <summary>
    /// 深度级别
    /// </summary>
    public class DepthLevel
    {
        /// <summary>
        /// 价格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public decimal Quantity { get; set; }
    }

    /// <summary>
    /// 最新成交
    /// </summary>
    public class RecentTrade
    {
        /// <summary>
        /// 交易ID
        /// </summary>
        public long TradeId { get; set; }

        /// <summary>
        /// 价格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 成交时间
        /// </summary>
        public DateTime TradeTime { get; set; }

        /// <summary>
        /// 是否买方主动成交
        /// </summary>
        public bool IsBuyerMaker { get; set; }
    }

    /// <summary>
    /// 标记价格
    /// </summary>
    public class MarkPrice
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 标记价格
        /// </summary>
        public decimal MarkPriceValue { get; set; }

        /// <summary>
        /// 指数价格
        /// </summary>
        public decimal IndexPrice { get; set; }

        /// <summary>
        /// 预估结算价格
        /// </summary>
        public decimal EstimatedSettlePrice { get; set; }

        /// <summary>
        /// 上次资金费率
        /// </summary>
        public decimal LastFundingRate { get; set; }

        /// <summary>
        /// 下次资金费率时间
        /// </summary>
        public DateTime NextFundingTime { get; set; }

        /// <summary>
        /// 利息率
        /// </summary>
        public decimal InterestRate { get; set; }

        /// <summary>
        /// 时间
        /// </summary>
        public DateTime Time { get; set; }
    }

    /// <summary>
    /// 资金费率历史
    /// </summary>
    public class FundingRateHistory
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 资金费率
        /// </summary>
        public decimal FundingRate { get; set; }

        /// <summary>
        /// 资金费率时间
        /// </summary>
        public DateTime FundingTime { get; set; }
    }

    /// <summary>
    /// 交易对信息
    /// </summary>
    public class ExchangeInfo
    {
        /// <summary>
        /// 时区
        /// </summary>
        public string Timezone { get; set; } = string.Empty;

        /// <summary>
        /// 服务器时间
        /// </summary>
        public DateTime ServerTime { get; set; }

        /// <summary>
        /// 交易对信息列表
        /// </summary>
        public List<SymbolInfo> Symbols { get; set; } = new List<SymbolInfo>();

        /// <summary>
        /// 交易规则
        /// </summary>
        public List<ExchangeFilter> ExchangeFilters { get; set; } = new List<ExchangeFilter>();
    }

    /// <summary>
    /// 交易所过滤器
    /// </summary>
    public class ExchangeFilter
    {
        /// <summary>
        /// 过滤器类型
        /// </summary>
        public string FilterType { get; set; } = string.Empty;

        /// <summary>
        /// 最大订单数量
        /// </summary>
        public int? MaxNumOrders { get; set; }

        /// <summary>
        /// 最大算法订单数量
        /// </summary>
        public int? MaxNumAlgoOrders { get; set; }

        /// <summary>
        /// 最大价格
        /// </summary>
        public decimal? MaxPrice { get; set; }

        /// <summary>
        /// 最小价格
        /// </summary>
        public decimal? MinPrice { get; set; }

        /// <summary>
        /// 价格精度
        /// </summary>
        public int? PricePrecision { get; set; }

        /// <summary>
        /// 数量精度
        /// </summary>
        public int? QtyPrecision { get; set; }
    }

    /// <summary>
    /// K线数据
    /// </summary>
    public class Kline
    {
        /// <summary>
        /// 交易对
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 开盘时间
        /// </summary>
        public DateTime OpenTime { get; set; }

        /// <summary>
        /// 开盘价
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// 最高价
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// 最低价
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// 收盘价
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// 成交量
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// 收盘时间
        /// </summary>
        public DateTime CloseTime { get; set; }

        /// <summary>
        /// 成交额
        /// </summary>
        public decimal QuoteVolume { get; set; }

        /// <summary>
        /// 成交笔数
        /// </summary>
        public long NumberOfTrades { get; set; }

        /// <summary>
        /// 主动买入成交量
        /// </summary>
        public decimal TakerBuyVolume { get; set; }

        /// <summary>
        /// 主动买入成交额
        /// </summary>
        public decimal TakerBuyQuoteVolume { get; set; }
    }


} 