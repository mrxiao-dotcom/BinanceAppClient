using BinanceApps.Core.Models;

namespace BinanceApps.Core.Interfaces
{
    /// <summary>
    /// 币安模拟API客户端接口
    /// 继承自基础API客户端，提供模拟交易功能
    /// </summary>
    public interface IBinanceSimulatedApiClient : IBinanceApiClient
    {
        /// <summary>
        /// 重置模拟账户数据
        /// </summary>
        /// <param name="initialBalance">初始余额（USDT）</param>
        Task ResetSimulatedAccountAsync(decimal initialBalance = 10000m);

        /// <summary>
        /// 设置模拟价格
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="price">价格</param>
        Task SetSimulatedPriceAsync(string symbol, decimal price);

        /// <summary>
        /// 获取模拟账户余额
        /// </summary>
        /// <returns>模拟账户余额</returns>
        Task<List<Balance>> GetSimulatedBalanceAsync();

        /// <summary>
        /// 获取模拟持仓信息
        /// </summary>
        /// <returns>模拟持仓信息</returns>
        Task<List<Position>> GetSimulatedPositionsAsync();

        /// <summary>
        /// 获取模拟订单列表
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="limit">限制数量</param>
        /// <returns>模拟订单列表</returns>
        Task<List<BaseOrder>> GetSimulatedOrdersAsync(string symbol, int limit = 500);

        /// <summary>
        /// 获取模拟K线数据
        /// </summary>
        /// <param name="symbol">交易对</param>
        /// <param name="interval">时间间隔</param>
        /// <param name="limit">限制数量</param>
        /// <returns>模拟K线数据</returns>
        Task<List<Kline>> GetSimulatedKlinesAsync(string symbol, KlineInterval interval, int limit = 500);

        /// <summary>
        /// 获取所有tick数据（24小时价格统计）
        /// </summary>
        Task<List<PriceStatistics>> GetAllTicksAsync();

        /// <summary>
        /// 获取所有可交易合约信息
        /// </summary>
        Task<List<SymbolInfo>> GetAllSymbolsInfoAsync();

        /// <summary>
        /// 从本地文件加载合约信息
        /// </summary>
        Task<List<SymbolInfo>> LoadSymbolsFromFileAsync();

        /// <summary>
        /// 保存合约信息到本地文件
        /// </summary>
        Task SaveSymbolsToFileAsync(List<SymbolInfo> symbols);

        /// <summary>
        /// 从币安交易所获取永续合约tick信息
        /// </summary>
        Task<List<PriceStatistics>> GetBinancePerpetualTicksAsync();

        /// <summary>
        /// 获取单个合约的详细信息
        /// </summary>
        Task<SymbolInfo> GetSymbolInfoAsync(string symbol);
    }
} 