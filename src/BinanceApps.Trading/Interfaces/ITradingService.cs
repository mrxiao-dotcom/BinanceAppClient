using BinanceApps.Core.Models;

namespace BinanceApps.Trading.Interfaces
{
    /// <summary>
    /// 交易服务接口
    /// </summary>
    public interface ITradingService
    {
        /// <summary>
        /// 下单
        /// </summary>
        /// <param name="order">订单信息</param>
        /// <returns>下单结果</returns>
        Task<PerpetualOrder> PlaceOrderAsync(PerpetualOrder order);

        /// <summary>
        /// 批量下单
        /// </summary>
        /// <param name="orders">订单列表</param>
        /// <returns>下单结果列表</returns>
        Task<List<PerpetualOrder>> PlaceBatchOrdersAsync(List<PerpetualOrder> orders);

        /// <summary>
        /// 取消订单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="orderId">订单ID</param>
        /// <returns>是否成功</returns>
        Task<bool> CancelOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 取消所有订单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>是否成功</returns>
        Task<bool> CancelAllOrdersAsync(string symbol);

        /// <summary>
        /// 查询订单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="orderId">订单ID</param>
        /// <returns>订单信息</returns>
        Task<PerpetualOrder?> GetOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 查询所有订单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <returns>订单列表</returns>
        Task<List<PerpetualOrder>> GetAllOrdersAsync(string symbol, int limit = 500);

        /// <summary>
        /// 查询当前订单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>当前订单列表</returns>
        Task<List<PerpetualOrder>> GetOpenOrdersAsync(string symbol);

        /// <summary>
        /// 设置杠杆
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="leverage">杠杆倍数</param>
        /// <returns>是否成功</returns>
        Task<bool> SetLeverageAsync(string symbol, int leverage);

        /// <summary>
        /// 设置保证金类型
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="marginType">保证金类型</param>
        /// <returns>是否成功</returns>
        Task<bool> SetMarginTypeAsync(string symbol, string marginType);

        /// <summary>
        /// 创建条件单
        /// </summary>
        /// <param name="order">条件单信息</param>
        /// <returns>条件单结果</returns>
        Task<ConditionalOrder> PlaceConditionalOrderAsync(ConditionalOrder order);

        /// <summary>
        /// 取消条件单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="orderId">条件单ID</param>
        /// <returns>是否成功</returns>
        Task<bool> CancelConditionalOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 查询条件单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="orderId">条件单ID</param>
        /// <returns>条件单信息</returns>
        Task<ConditionalOrder?> GetConditionalOrderAsync(string symbol, long orderId);

        /// <summary>
        /// 查询所有条件单
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>条件单列表</returns>
        Task<List<ConditionalOrder>> GetAllConditionalOrdersAsync(string symbol);

        /// <summary>
        /// 获取交易对信息
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>交易对信息</returns>
        Task<SymbolInfo?> GetSymbolInfoAsync(string symbol);

        /// <summary>
        /// 获取所有交易对信息
        /// </summary>
        /// <returns>交易对信息列表</returns>
        Task<List<SymbolInfo>> GetAllSymbolsInfoAsync();

        /// <summary>
        /// 验证订单参数
        /// </summary>
        /// <param name="order">订单信息</param>
        /// <returns>验证结果</returns>
        Task<(bool IsValid, string ErrorMessage)> ValidateOrderAsync(PerpetualOrder order);
    }
} 