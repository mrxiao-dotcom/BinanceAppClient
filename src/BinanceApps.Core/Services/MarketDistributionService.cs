using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 市场涨幅分布分析服务
    /// </summary>
    public class MarketDistributionService
    {
        private readonly ILogger<MarketDistributionService> _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly TickerCacheService _tickerCacheService;
        private readonly SymbolInfoCacheService _symbolInfoCacheService;

        public MarketDistributionService(
            ILogger<MarketDistributionService> logger,
            IBinanceSimulatedApiClient apiClient,
            KlineDataStorageService klineStorageService,
            TickerCacheService tickerCacheService,
            SymbolInfoCacheService symbolInfoCacheService)
        {
            _logger = logger;
            _apiClient = apiClient;
            _klineStorageService = klineStorageService;
            _tickerCacheService = tickerCacheService;
            _symbolInfoCacheService = symbolInfoCacheService;
        }

        /// <summary>
        /// 获取最近N天的涨幅分布数据
        /// </summary>
        public async Task<MarketDistributionAnalysisResult> GetDistributionAsync(int days = 5)
        {
            _logger.LogInformation($"开始分析最近 {days} 天的市场涨幅分布");

            var result = new MarketDistributionAnalysisResult
            {
                Days = days
            };

            try
            {
                // 从缓存获取所有活跃交易的合约列表
                var symbolsInfo = await _symbolInfoCacheService.GetAllSymbolsInfoAsync();
                var activeSymbols = symbolsInfo
                    .Where(s => s.IsTrading && s.Symbol.EndsWith("USDT") && s.ContractType == Models.ContractType.Perpetual)
                    .Select(s => s.Symbol)
                    .ToList();

                _logger.LogInformation($"获取到 {activeSymbols.Count} 个活跃合约");

                // 计算每一天的分布
                for (int i = 0; i < days; i++)
                {
                    var targetDate = DateTime.Today.AddDays(-i);
                    var isToday = (i == 0);

                    DailyPriceChangeDistribution distribution;

                    if (isToday)
                    {
                        // 今天使用实时24H ticker数据
                        distribution = await CalculateTodayDistributionAsync(activeSymbols);
                    }
                    else
                    {
                        // 历史日期使用K线数据
                        distribution = await CalculateHistoricalDistributionAsync(activeSymbols, targetDate);
                    }

                    distribution.Date = targetDate;
                    distribution.IsToday = isToday;
                    result.DailyDistributions.Add(distribution);

                    _logger.LogInformation($"完成 {targetDate:yyyy-MM-dd} 的分布计算，总合约数: {distribution.TotalSymbols}");
                }

                _logger.LogInformation($"市场涨幅分布分析完成，共 {result.DailyDistributions.Count} 天");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分析市场涨幅分布时发生错误");
            }

            return result;
        }

        /// <summary>
        /// 计算今天的涨幅分布（使用实时ticker数据）
        /// </summary>
        private async Task<DailyPriceChangeDistribution> CalculateTodayDistributionAsync(List<string> symbols)
        {
            var distribution = new DailyPriceChangeDistribution();

            // 初始化所有档位计数为0
            foreach (var range in DailyPriceChangeDistribution.GetAllRanges())
            {
                distribution.RangeCounts[range] = 0;
            }

            try
            {
                // 从缓存获取所有合约的24H ticker数据
                var tickers = await _tickerCacheService.GetAllTickersAsync();
                var symbolSet = new HashSet<string>(symbols);

                foreach (var ticker in tickers)
                {
                    // 只统计活跃合约
                    if (!symbolSet.Contains(ticker.Symbol))
                        continue;

                    // 判断涨跌幅所属档位
                    var range = DailyPriceChangeDistribution.GetRange(ticker.PriceChangePercent);
                    distribution.RangeCounts[range]++;
                    distribution.TotalSymbols++;
                }

                _logger.LogInformation($"今日实时数据：总合约 {distribution.TotalSymbols} 个");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算今日涨幅分布失败");
            }

            return distribution;
        }

        /// <summary>
        /// 计算历史某一天的涨幅分布（使用K线数据）
        /// </summary>
        private async Task<DailyPriceChangeDistribution> CalculateHistoricalDistributionAsync(
            List<string> symbols,
            DateTime date)
        {
            var distribution = new DailyPriceChangeDistribution();

            // 初始化所有档位计数为0
            foreach (var range in DailyPriceChangeDistribution.GetAllRanges())
            {
                distribution.RangeCounts[range] = 0;
            }

            try
            {
                int successCount = 0;
                int noDataCount = 0;
                int errorCount = 0;

                foreach (var symbol in symbols)
                {
                    try
                    {
                        // 从本地加载K线数据
                        var (klines, success, errorMsg) = await _klineStorageService.LoadKlineDataAsync(symbol);

                        if (!success || klines == null || klines.Count < 2)
                        {
                            noDataCount++;
                            continue;
                        }

                        // 按时间排序（升序）
                        var sortedKlines = klines.OrderBy(k => k.OpenTime).ToList();

                        // 找到目标日期及其前一天的K线（使用更宽松的日期匹配）
                        // 目标日期：date的当天（0:00 到 23:59）
                        var targetDayStart = date.Date;
                        var targetDayEnd = date.Date.AddDays(1);
                        
                        // 前一天：date前一天的当天（0:00 到 23:59）
                        var previousDayStart = date.Date.AddDays(-1);
                        var previousDayEnd = date.Date;

                        // 查找目标日期的K线（取最后一根，通常是收盘价）
                        var targetKlines = sortedKlines
                            .Where(k => k.OpenTime >= targetDayStart && k.OpenTime < targetDayEnd)
                            .ToList();

                        // 查找前一天的K线（取最后一根，通常是收盘价）
                        var previousKlines = sortedKlines
                            .Where(k => k.OpenTime >= previousDayStart && k.OpenTime < previousDayEnd)
                            .ToList();

                        if (targetKlines.Count == 0 || previousKlines.Count == 0)
                        {
                            noDataCount++;
                            continue;
                        }

                        var targetKline = targetKlines.Last(); // 取当天最后一根K线
                        var previousKline = previousKlines.Last(); // 取前一天最后一根K线

                        // 计算涨跌幅：(当日收盘 - 前日收盘) / 前日收盘 * 100%
                        if (previousKline.ClosePrice == 0)
                        {
                            errorCount++;
                            continue;
                        }

                        var priceChangePercent = ((targetKline.ClosePrice - previousKline.ClosePrice) / previousKline.ClosePrice) * 100m;

                        // 判断所属档位
                        var range = DailyPriceChangeDistribution.GetRange(priceChangePercent);
                        distribution.RangeCounts[range]++;
                        distribution.TotalSymbols++;
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _logger.LogDebug(ex, $"处理 {symbol} 的 {date:yyyy-MM-dd} 数据失败");
                    }
                }

                _logger.LogInformation($"{date:yyyy-MM-dd} 历史数据：总合约 {distribution.TotalSymbols} 个 (成功={successCount}, 无数据={noDataCount}, 错误={errorCount})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算 {date:yyyy-MM-dd} 涨幅分布失败");
            }

            return distribution;
        }
    }
}

