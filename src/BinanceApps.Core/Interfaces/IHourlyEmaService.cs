using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BinanceApps.Core.Models;

namespace BinanceApps.Core.Interfaces
{
    /// <summary>
    /// 小时均线监控服务接口
    /// </summary>
    public interface IHourlyEmaService
    {
        /// <summary>
        /// 获取所有可交易合约的小时K线数据
        /// </summary>
        /// <param name="parameters">监控参数</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>是否成功</returns>
        Task<bool> FetchHourlyKlinesAsync(HourlyEmaParameters parameters, Action<HourlyKlineDownloadProgress>? progressCallback = null);

        /// <summary>
        /// 增量更新K线数据（从最后一个K线到现在）
        /// </summary>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateHourlyKlinesAsync(Action<HourlyKlineDownloadProgress>? progressCallback = null);

        /// <summary>
        /// 检查K线是否在最近1小时内，不是则增量更新
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> CheckAndUpdateKlinesIfNeededAsync();

        /// <summary>
        /// 用Ticker价格更新所有合约最后一个K线的收盘价（仅缓存）
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> UpdateLastKlineWithTickerAsync();

        /// <summary>
        /// 计算EMA均线数据
        /// </summary>
        /// <param name="parameters">监控参数</param>
        /// <returns>是否成功</returns>
        Task<bool> CalculateEmaAsync(HourlyEmaParameters parameters);

        /// <summary>
        /// 计算连续大于/小于EMA的K线数量
        /// </summary>
        /// <returns>是否成功</returns>
        Task<bool> CalculateAboveBelowEmaCountsAsync();

        /// <summary>
        /// 获取所有合约的监控结果
        /// </summary>
        /// <param name="filter">筛选条件（可选）</param>
        /// <returns>监控结果列表</returns>
        Task<List<HourlyEmaMonitorResult>> GetMonitorResultsAsync(HourlyEmaFilter? filter = null);

        /// <summary>
        /// 获取指定合约的K线和EMA数据
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <returns>K线和EMA数据</returns>
        Task<HourlyKlineData?> GetHourlyKlineDataAsync(string symbol);

        /// <summary>
        /// 更新指定合约的最新价格并重新计算EMA（用于浮动监控窗口）
        /// </summary>
        /// <param name="symbol">合约名称</param>
        /// <param name="latestPrice">最新价格</param>
        /// <param name="emaPeriod">EMA周期</param>
        /// <returns>是否成功</returns>
        Task<bool> UpdateSymbolLatestPriceAndEmaAsync(string symbol, decimal latestPrice, int emaPeriod = 26);

        /// <summary>
        /// 清除所有缓存数据
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 获取缓存中的合约数量
        /// </summary>
        /// <returns>合约数量</returns>
        int GetCachedSymbolCount();

        /// <summary>
        /// 获取最后一个K线的时间距离现在的小时数
        /// </summary>
        /// <returns>小时数</returns>
        double GetHoursSinceLastKline();
    }
}

