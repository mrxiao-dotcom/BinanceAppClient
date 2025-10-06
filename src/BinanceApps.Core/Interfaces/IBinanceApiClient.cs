using BinanceApps.Core.Models;

namespace BinanceApps.Core.Interfaces
{
    /// <summary>
    /// 币安API客户端基础接口
    /// </summary>
    public interface IBinanceApiClient
    {
        /// <summary>
        /// API密钥
        /// </summary>
        string ApiKey { get; set; }

        /// <summary>
        /// 密钥
        /// </summary>
        string SecretKey { get; set; }

        /// <summary>
        /// 是否使用测试网络
        /// </summary>
        bool IsTestnet { get; set; }

        /// <summary>
        /// 基础URL
        /// </summary>
        string BaseUrl { get; }

        /// <summary>
        /// 初始化客户端
        /// </summary>
        /// <param name="apiKey">API密钥</param>
        /// <param name="secretKey">密钥</param>
        /// <param name="isTestnet">是否使用测试网络</param>
        Task InitializeAsync(string apiKey, string secretKey, bool isTestnet = false);

        /// <summary>
        /// 测试连接
        /// </summary>
        /// <returns>连接是否成功</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// 获取服务器时间
        /// </summary>
        /// <returns>服务器时间</returns>
        Task<DateTime> GetServerTimeAsync();

        /// <summary>
        /// 获取账户信息
        /// </summary>
        /// <returns>账户信息</returns>
        Task<AccountInfo> GetAccountInfoAsync();

        /// <summary>
        /// 获取账户余额
        /// </summary>
        /// <returns>账户余额列表</returns>
        Task<List<Balance>> GetAccountBalanceAsync();

        /// <summary>
        /// 获取持仓信息
        /// </summary>
        /// <returns>持仓信息列表</returns>
        Task<List<Position>> GetPositionsAsync();

        /// <summary>
        /// 下单
        /// </summary>
        /// <param name="order">订单信息</param>
        /// <returns>订单结果</returns>
        Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest order);

        /// <summary>
        /// 取消订单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="orderId">订单ID</param>
        /// <returns>取消结果</returns>
        Task<CancelOrderResult> CancelOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 获取订单信息
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="orderId">订单ID</param>
        /// <returns>订单信息</returns>
        Task<BaseOrder> GetOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 获取订单列表
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <returns>订单列表</returns>
        Task<List<BaseOrder>> GetOrdersAsync(string symbol, int limit = 500);

        /// <summary>
        /// 获取K线数据
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="interval">时间间隔</param>
        /// <param name="limit">限制数量</param>
        /// <returns>K线数据列表</returns>
        Task<List<Kline>> GetKlinesAsync(string symbol, KlineInterval interval, int limit = 500);

        /// <summary>
        /// 获取24小时价格统计
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>24小时价格统计</returns>
        Task<PriceStatistics> Get24hrPriceStatisticsAsync(string symbol);

        /// <summary>
        /// 获取最新价格
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>最新价格</returns>
        Task<decimal> GetLatestPriceAsync(string symbol);
    }
} 