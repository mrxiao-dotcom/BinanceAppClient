using BinanceApps.Core.Models;

namespace BinanceApps.MarketData.Interfaces
{
    /// <summary>
    /// 行情数据服务接口
    /// </summary>
    public interface IMarketDataService
    {
        /// <summary>
        /// 获取K线数据
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="interval">时间间隔</param>
        /// <param name="limit">限制数量</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>K线数据列表</returns>
        Task<List<KlineData>> GetKlinesAsync(string symbol, KlineInterval interval, int limit = 500, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// 获取24小时价格统计
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>价格统计</returns>
        Task<PriceStatistics?> GetPriceStatisticsAsync(string symbol);

        /// <summary>
        /// 获取所有交易对24小时价格统计
        /// </summary>
        /// <returns>价格统计列表</returns>
        Task<List<PriceStatistics>> GetAllPriceStatisticsAsync();

        /// <summary>
        /// 获取深度数据
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">深度级别</param>
        /// <returns>深度数据</returns>
        Task<DepthData?> GetDepthAsync(string symbol, int limit = 100);

        /// <summary>
        /// 获取最新成交
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <returns>最新成交列表</returns>
        Task<List<RecentTrade>> GetRecentTradesAsync(string symbol, int limit = 500);

        /// <summary>
        /// 获取历史成交
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <param name="fromId">起始交易ID</param>
        /// <returns>历史成交列表</returns>
        Task<List<RecentTrade>> GetHistoricalTradesAsync(string symbol, int limit = 1000, long? fromId = null);

        /// <summary>
        /// 获取标记价格
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>标记价格</returns>
        Task<MarkPrice?> GetMarkPriceAsync(string symbol);

        /// <summary>
        /// 获取所有交易对标记价格
        /// </summary>
        /// <returns>标记价格列表</returns>
        Task<List<MarkPrice>> GetAllMarkPricesAsync();

        /// <summary>
        /// 获取资金费率
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>资金费率</returns>
        Task<FundingRate?> GetFundingRateAsync(string symbol);

        /// <summary>
        /// 获取所有资金费率
        /// </summary>
        /// <returns>资金费率列表</returns>
        Task<List<FundingRate>> GetAllFundingRatesAsync();

        /// <summary>
        /// 获取资金费率历史
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>资金费率历史</returns>
        Task<List<FundingRateHistory>> GetFundingRateHistoryAsync(string symbol, int limit = 100, DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// 获取交易所信息
        /// </summary>
        /// <returns>交易所信息</returns>
        Task<ExchangeInfo?> GetExchangeInfoAsync();

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
        /// 订阅实时价格
        /// </summary>
        /// <param name="symbols">交易对列表</param>
        /// <param name="onPriceUpdate">价格更新回调</param>
        /// <returns>订阅ID</returns>
        Task<string> SubscribeToPriceUpdatesAsync(List<string> symbols, Action<PriceStatistics> onPriceUpdate);

        /// <summary>
        /// 订阅K线数据
        /// </summary>
        /// <param name="symbols">交易对列表</param>
        /// <param name="interval">时间间隔</param>
        /// <param name="onKlineUpdate">K线更新回调</param>
        /// <returns>订阅ID</returns>
        Task<string> SubscribeToKlineUpdatesAsync(List<string> symbols, KlineInterval interval, Action<KlineData> onKlineUpdate);

        /// <summary>
        /// 订阅深度数据
        /// </summary>
        /// <param name="symbols">交易对列表</param>
        /// <param name="onDepthUpdate">深度更新回调</param>
        /// <returns>订阅ID</returns>
        Task<string> SubscribeToDepthUpdatesAsync(List<string> symbols, Action<DepthData> onDepthUpdate);

        /// <summary>
        /// 取消订阅
        /// </summary>
        /// <param name="subscriptionId">订阅ID</param>
        /// <returns>是否成功</returns>
        Task<bool> UnsubscribeAsync(string subscriptionId);

        /// <summary>
        /// 取消所有订阅
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> UnsubscribeAllAsync();
    }
} 