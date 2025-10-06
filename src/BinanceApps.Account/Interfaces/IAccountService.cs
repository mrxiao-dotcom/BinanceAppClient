using BinanceApps.Core.Models;

namespace BinanceApps.Account.Interfaces
{
    /// <summary>
    /// 账户服务接口
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// 获取账户信息
        /// </summary>
        /// <returns>账户信息</returns>
        Task<AccountInfo> GetAccountInfoAsync();

        /// <summary>
        /// 获取账户余额
        /// </summary>
        /// <returns>余额列表</returns>
        Task<List<Balance>> GetBalancesAsync();

        /// <summary>
        /// 获取指定资产余额
        /// </summary>
        /// <param name="asset">资产名称</param>
        /// <returns>余额信息</returns>
        Task<Balance?> GetBalanceAsync(string asset);

        /// <summary>
        /// 获取持仓信息
        /// </summary>
        /// <param name="symbol">交易对，为空则获取所有</param>
        /// <returns>持仓列表</returns>
        Task<List<Position>> GetPositionsAsync(string? symbol = null);

        /// <summary>
        /// 获取指定交易对持仓
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>持仓信息</returns>
        Task<Position?> GetPositionAsync(string symbol);

        /// <summary>
        /// 获取交易历史
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <param name="fromId">起始交易ID</param>
        /// <returns>交易历史列表</returns>
        Task<List<TradeHistory>> GetTradeHistoryAsync(string symbol, int limit = 500, long? fromId = null);

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
        /// 获取杠杆信息
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>杠杆信息</returns>
        Task<LeverageInfo?> GetLeverageInfoAsync(string symbol);

        /// <summary>
        /// 获取风险信息
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <returns>风险信息</returns>
        Task<RiskInfo?> GetRiskInfoAsync(string symbol);

        /// <summary>
        /// 获取账户风险摘要
        /// </summary>
        /// <returns>风险摘要</returns>
        Task<Dictionary<string, RiskInfo>> GetAccountRiskSummaryAsync();

        /// <summary>
        /// 获取账户统计信息
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="periodType">统计周期类型</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>统计信息</returns>
        Task<object> GetAccountStatisticsAsync(string symbol, string periodType = "DAILY", DateTime? startTime = null, DateTime? endTime = null);

        /// <summary>
        /// 获取账户交易统计
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>交易统计</returns>
        Task<object> GetTradeStatisticsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null);
    }
} 