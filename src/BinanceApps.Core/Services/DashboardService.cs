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
    /// 仪表板服务 - 综合市场分析
    /// </summary>
    public class DashboardService
    {
        private readonly ILogger<DashboardService> _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly MarketPositionService _positionService;
        private readonly MaDistanceService _maService;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly ContractInfoService _contractInfoService;
        
        public DashboardService(
            ILogger<DashboardService> logger,
            IBinanceSimulatedApiClient apiClient,
            MarketPositionService positionService,
            MaDistanceService maService,
            KlineDataStorageService klineStorageService,
            ContractInfoService contractInfoService)
        {
            _logger = logger;
            _apiClient = apiClient;
            _positionService = positionService;
            _maService = maService;
            _klineStorageService = klineStorageService;
            _contractInfoService = contractInfoService;
        }
        
        /// <summary>
        /// 获取仪表板综合数据
        /// </summary>
        public async Task<DashboardSummary> GetDashboardSummaryAsync(int positionDays = 30, int maPeriod = 20, decimal maThreshold = 5m)
        {
            _logger.LogInformation("开始生成仪表板数据...");
            
            var summary = new DashboardSummary
            {
                UpdateTime = DateTime.Now
            };
            
            try
            {
                // 1. 获取所有可交易的合约信息（过滤掉已下架的合约）
                _logger.LogInformation("正在获取可交易合约列表...");
                var allSymbols = await _apiClient.GetAllSymbolsInfoAsync();
                var tradingSymbols = new HashSet<string>();
                
                if (allSymbols != null && allSymbols.Count > 0)
                {
                    tradingSymbols = allSymbols
                        .Where(s => s.IsTrading && s.QuoteAsset == "USDT" && s.ContractType == ContractType.Perpetual)
                        .Select(s => s.Symbol)
                        .ToHashSet();
                    _logger.LogInformation($"找到 {tradingSymbols.Count} 个可交易的USDT永续合约");
                }
                
                // 2. 获取ticker数据
                var allTickers = await _apiClient.GetAllTicksAsync();
                _logger.LogInformation($"获取到 {allTickers.Count} 个合约的ticker数据");
                
                // 3. 只保留可交易的合约（过滤掉下架品种）
                var tickers = allTickers;
                if (tradingSymbols.Count > 0)
                {
                    var originalCount = allTickers.Count;
                    tickers = allTickers.Where(t => tradingSymbols.Contains(t.Symbol)).ToList();
                    var filteredCount = originalCount - tickers.Count;
                    _logger.LogInformation($"过滤掉 {filteredCount} 个不可交易或非永续合约，剩余 {tickers.Count} 个");
                }
                
                // 4. 获取高低价位置统计
                summary.PositionStats = await GetPositionDistributionAsync(positionDays);
                
                // 5. 获取24H市场动态
                summary.MarketStats = GetMarketDynamics(tickers);
                
                // 6. 获取均线距离统计
                summary.MaStats = await GetMaDistanceDistributionAsync(maPeriod, maThreshold);
                
                // 7. 获取量比排行TOP20
                summary.VolumeRatioTop20 = GetVolumeRatioTop20(tickers);
                
                // 8. 获取30天从最低价涨幅TOP20
                summary.Top20GainsFrom30DayLow = await Get30DayLowGainsTop20Async(tickers);
                
                // 9. 获取30天从最高价跌幅TOP20
                summary.Top20FallsFrom30DayHigh = await Get30DayHighFallsTop20Async(tickers);
                
                // 10. 综合分析市场趋势
                summary.TrendAnalysis = AnalyzeMarketTrend(summary);
                
                _logger.LogInformation($"仪表板数据生成完成: {summary.TrendAnalysis.TrendDescription}");
                
                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成仪表板数据时发生错误");
                throw;
            }
        }
        
        /// <summary>
        /// 获取高低价位置分布
        /// </summary>
        private async Task<PositionDistribution> GetPositionDistributionAsync(int analysisDays)
        {
            try
            {
                // 获取今天的市场位置数据
                var today = DateTime.UtcNow.Date;
                var locationData = await CalculateLocationDataAsync(today, analysisDays);
                
                // 统计各区域数量（LocationRatio 是 0-1 之间的小数，需要转换为百分比比较）
                var highCount = locationData.Count(d => d.LocationRatio > 0.75m);
                var midHighCount = locationData.Count(d => d.LocationRatio > 0.50m && d.LocationRatio <= 0.75m);
                var midLowCount = locationData.Count(d => d.LocationRatio > 0.25m && d.LocationRatio <= 0.50m);
                var lowCount = locationData.Count(d => d.LocationRatio <= 0.25m);
                
                return new PositionDistribution
                {
                    HighCount = highCount,
                    MidHighCount = midHighCount,
                    MidLowCount = midLowCount,
                    LowCount = lowCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取位置分布数据失败");
                return new PositionDistribution();
            }
        }
        
        /// <summary>
        /// 计算指定日期的位置数据
        /// </summary>
        private async Task<List<LocationData>> CalculateLocationDataAsync(DateTime date, int analysisDays)
        {
            var result = new List<LocationData>();
            
            try
            {
                // 获取所有合约的ticker数据
                var tickers = await _apiClient.GetAllTicksAsync();
                
                foreach (var ticker in tickers)
                {
                    try
                    {
                        // 加载K线数据
                        var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(ticker.Symbol);
                        if (!loadSuccess || klines == null || klines.Count == 0) continue;
                        
                        // 基于指定日期动态计算时间范围
                        var endDate = date.AddDays(1); // 包含当天
                        var startDate = endDate.AddDays(-analysisDays);
                        
                        var filteredKlines = klines
                            .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date < endDate.Date)
                            .OrderBy(k => k.OpenTime)
                            .ToList();
                            
                        if (filteredKlines.Count == 0) continue;
                        
                        // 计算该时间段的最高最低价
                        var highestPrice = filteredKlines.Max(k => k.HighPrice);
                        var lowestPrice = filteredKlines.Min(k => k.LowPrice);
                        var priceRange = highestPrice - lowestPrice;
                        
                        if (priceRange <= 0) continue;
                        
                        // 获取指定日期的收盘价（使用最新的K线或ticker价格）
                        var dayKline = filteredKlines.LastOrDefault(k => k.OpenTime.Date == date.Date);
                        var currentPrice = dayKline?.ClosePrice ?? ticker.LastPrice;
                        
                        var locationRatio = (currentPrice - lowestPrice) / priceRange;
                        
                        // 确定状态
                        string status = locationRatio switch
                        {
                            <= 0.25m => "低位区域",
                            <= 0.50m => "中低区域", 
                            <= 0.75m => "中高区域",
                            _ => "高位区域"
                        };
                        
                        result.Add(new LocationData
                        {
                            Symbol = ticker.Symbol,
                            CurrentPrice = currentPrice,
                            LocationRatio = locationRatio,
                            HighestPrice = highestPrice,
                            LowestPrice = lowestPrice,
                            PriceRange = priceRange,
                            Status = status
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"计算 {ticker.Symbol} 位置数据失败: {ex.Message}");
                    }
                }
                
                _logger.LogInformation($"完成位置数据计算，共 {result.Count} 个合约");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"计算 {date:yyyy-MM-dd} 位置数据失败");
                return result;
            }
        }
        
        /// <summary>
        /// 获取24H市场动态
        /// </summary>
        private MarketDynamics GetMarketDynamics(List<PriceStatistics> tickers)
        {
            var dynamics = new MarketDynamics
            {
                TotalSymbolCount = tickers.Count
            };
            
            // 计算总成交额
            dynamics.TotalVolume = tickers.Sum(t => t.QuoteVolume);
            
            // 统计涨跌合约数量
            dynamics.RisingCount = tickers.Count(t => t.PriceChangePercent > 0);
            dynamics.FallingCount = tickers.Count(t => t.PriceChangePercent < 0);
            dynamics.FlatCount = tickers.Count(t => t.PriceChangePercent == 0);
            
            // 统计高波动合约数量（绝对值>3%）
            dynamics.HighVolatilityCount = tickers.Count(t => Math.Abs(t.PriceChangePercent) > 3);
            
            // 获取24H最大涨幅和跌幅 - 严格过滤有效交易数据
            // 过滤条件：
            // 1. 24H成交额 > 2000万USDT（更严格，彻底排除下架和冷门币种）
            // 2. 当前价格 > 0
            // 3. 24H成交量 > 0
            // 4. 涨跌幅绝对值 > 0.01%（排除几乎无波动的异常数据）
            var minVolumeThreshold = 20_000_000m; // 2000万USDT最低成交额阈值（更严格标准）
            
            var validTickers = tickers
                .Where(t => 
                    t.QuoteVolume > minVolumeThreshold && 
                    t.LastPrice > 0 && 
                    t.Volume > 0 &&
                    Math.Abs(t.PriceChangePercent) > 0.01m)
                .ToList();
            
            _logger.LogInformation($"过滤前合约数: {tickers.Count}, 过滤后活跃合约数: {validTickers.Count}");
            
            // 获取24H最大涨幅TOP10（只包含涨幅>0的合约）
            dynamics.TopGainers = validTickers
                .Where(t => t.PriceChangePercent > 0)
                .OrderByDescending(t => t.PriceChangePercent)
                .Take(10)
                .Select(t => new VolatilityItem
                {
                    Symbol = t.Symbol,
                    ChangePercent = t.PriceChangePercent
                })
                .ToList();
            
            // 获取24H最大跌幅TOP10（只包含跌幅<0的合约）
            dynamics.TopLosers = validTickers
                .Where(t => t.PriceChangePercent < 0)
                .OrderBy(t => t.PriceChangePercent) // 升序排列，最小的（跌得最多）在前
                .Take(10)
                .Select(t => new VolatilityItem
                {
                    Symbol = t.Symbol,
                    ChangePercent = t.PriceChangePercent
                })
                .ToList();
            
            // 记录获取结果
            _logger.LogInformation($"获取到涨幅TOP {dynamics.TopGainers.Count} 个，跌幅TOP {dynamics.TopLosers.Count} 个（成交额均 > 2000万USDT）");
            
            if (dynamics.TopGainers.Count == 0 && dynamics.TopLosers.Count == 0)
            {
                _logger.LogWarning("未找到符合条件的主流活跃合约（成交额 > 2000万USDT）");
            }
            
            // 判断成交额位置（简单判断）
            dynamics.VolumePosition = dynamics.TotalVolume > 50_000_000_000m ? "↑高位"
                : dynamics.TotalVolume > 30_000_000_000m ? "→中等"
                : "↓低位";
            
            return dynamics;
        }
        
        /// <summary>
        /// 获取均线距离分布
        /// </summary>
        private async Task<MaDistanceDistribution> GetMaDistanceDistributionAsync(int period, decimal threshold)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var result = await _maService.CalculateMaDistanceAsync(today, period, threshold);
                
                return new MaDistanceDistribution
                {
                    Period = period,
                    Threshold = threshold,
                    AboveFarCount = result.AboveFar.Count,
                    AboveNearCount = result.AboveNear.Count,
                    BelowNearCount = result.BelowNear.Count,
                    BelowFarCount = result.BelowFar.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取均线距离分布失败");
                return new MaDistanceDistribution { Period = period, Threshold = threshold };
            }
        }
        
        /// <summary>
        /// 综合分析市场趋势
        /// </summary>
        private MarketTrendAnalysis AnalyzeMarketTrend(DashboardSummary summary)
        {
            var analysis = new MarketTrendAnalysis();
            
            // 1. 均线信号分析
            analysis.MaSignal = AnalyzeMaSignal(summary.MaStats);
            
            // 2. 位置信号分析
            analysis.PositionSignal = AnalyzePositionSignal(summary.PositionStats);
            
            // 3. 涨跌信号分析
            analysis.ChangeSignal = AnalyzeChangeSignal(summary.MarketStats);
            
            // 4. 波动信号分析
            analysis.VolatilitySignal = AnalyzeVolatilitySignal(summary.MarketStats);
            
            // 5. 统计牛市信号数量
            analysis.BullishSignalCount = new[]
            {
                analysis.MaSignal.Signal,
                analysis.PositionSignal.Signal,
                analysis.ChangeSignal.Signal,
                analysis.VolatilitySignal.Signal
            }.Count(s => s == MarketSignal.Bullish);
            
            // 6. 确定综合趋势
            analysis.OverallTrend = analysis.BullishSignalCount switch
            {
                4 => MarketTrend.StrongBullish,
                3 => MarketTrend.Bullish,
                2 => MarketTrend.Sideways,
                1 => MarketTrend.Bearish,
                _ => MarketTrend.StrongBearish
            };
            
            // 7. 生成操作建议
            analysis.Suggestions = GenerateSuggestions(analysis.OverallTrend, summary);
            
            return analysis;
        }
        
        /// <summary>
        /// 分析均线信号
        /// </summary>
        private SignalDetail AnalyzeMaSignal(MaDistanceDistribution ma)
        {
            var signal = new SignalDetail { Name = "均线信号" };
            
            if (ma.AboveRatio > 50)
            {
                signal.Signal = MarketSignal.Bullish;
                signal.Description = "牛市";
                signal.RawData = $"均线之上:{ma.AboveRatio:F1}%，之下:{ma.BelowRatio:F1}%";
            }
            else if (ma.AboveRatio < 45)
            {
                signal.Signal = MarketSignal.Bearish;
                signal.Description = "熊市";
                signal.RawData = $"均线之上:{ma.AboveRatio:F1}%，之下:{ma.BelowRatio:F1}%";
            }
            else
            {
                signal.Signal = MarketSignal.Neutral;
                signal.Description = "中性";
                signal.RawData = $"均线之上:{ma.AboveRatio:F1}%，之下:{ma.BelowRatio:F1}%";
            }
            
            return signal;
        }
        
        /// <summary>
        /// 分析位置信号
        /// </summary>
        private SignalDetail AnalyzePositionSignal(PositionDistribution position)
        {
            var signal = new SignalDetail { Name = "位置信号" };
            
            if (position.HighRatio > 50)
            {
                signal.Signal = MarketSignal.Bullish;
                signal.Description = "牛市";
                signal.RawData = $"高位占比:{position.HighRatio:F1}%，低位占比:{position.LowRatio:F1}%";
            }
            else if (position.LowRatio > 50)
            {
                signal.Signal = MarketSignal.Bearish;
                signal.Description = "熊市";
                signal.RawData = $"高位占比:{position.HighRatio:F1}%，低位占比:{position.LowRatio:F1}%";
            }
            else
            {
                signal.Signal = MarketSignal.Neutral;
                signal.Description = "中性";
                signal.RawData = $"高位占比:{position.HighRatio:F1}%，低位占比:{position.LowRatio:F1}%";
            }
            
            return signal;
        }
        
        /// <summary>
        /// 分析涨跌信号
        /// </summary>
        private SignalDetail AnalyzeChangeSignal(MarketDynamics market)
        {
            var signal = new SignalDetail { Name = "涨跌信号" };
            
            if (market.RisingRatio > 55)
            {
                signal.Signal = MarketSignal.Bullish;
                signal.Description = "牛市";
                signal.RawData = $"上涨:{market.RisingCount}个，下跌:{market.FallingCount}个 | 比例:{market.RisingRatio:F1}%";
            }
            else if (market.RisingRatio < 45)
            {
                signal.Signal = MarketSignal.Bearish;
                signal.Description = "熊市";
                signal.RawData = $"上涨:{market.RisingCount}个，下跌:{market.FallingCount}个 | 比例:{market.RisingRatio:F1}%";
            }
            else
            {
                signal.Signal = MarketSignal.Neutral;
                signal.Description = "中性";
                signal.RawData = $"上涨:{market.RisingCount}个，下跌:{market.FallingCount}个 | 比例:{market.RisingRatio:F1}%";
            }
            
            return signal;
        }
        
        /// <summary>
        /// 分析波动信号
        /// </summary>
        private SignalDetail AnalyzeVolatilitySignal(MarketDynamics market)
        {
            var signal = new SignalDetail { Name = "波动信号" };
            
            if (market.HighVolatilityRatio > 50)
            {
                signal.Signal = MarketSignal.Bullish;
                signal.Description = "牛市";
                signal.RawData = $"高波动:{market.HighVolatilityRatio:F1}%，低波动:{(100 - market.HighVolatilityRatio):F1}%";
            }
            else if (market.HighVolatilityRatio < 40)
            {
                signal.Signal = MarketSignal.Bearish;
                signal.Description = "熊市";
                signal.RawData = $"高波动:{market.HighVolatilityRatio:F1}%，低波动:{(100 - market.HighVolatilityRatio):F1}%";
            }
            else
            {
                signal.Signal = MarketSignal.Neutral;
                signal.Description = "中性";
                signal.RawData = $"高波动:{market.HighVolatilityRatio:F1}%，低波动:{(100 - market.HighVolatilityRatio):F1}%";
            }
            
            return signal;
        }
        
        /// <summary>
        /// 生成操作建议
        /// </summary>
        private List<string> GenerateSuggestions(MarketTrend trend, DashboardSummary summary)
        {
            var suggestions = new List<string>();
            
            switch (trend)
            {
                case MarketTrend.StrongBullish:
                    suggestions.Add("✅ 积极做多，把握趋势");
                    suggestions.Add("✅ 追涨强势币种，山寨币可大胆持有");
                    suggestions.Add("✅ 持续上涨中，保持仓位");
                    suggestions.Add("⚠️ 避免做空，顺势而为");
                    break;
                    
                case MarketTrend.Bullish:
                    suggestions.Add("✅ 跟随趋势，做突破最强的币种");
                    suggestions.Add("✅ 持有主流币为主，山寨币为辅");
                    suggestions.Add("✅ 趋势向上，保持乐观");
                    suggestions.Add("⚠️ 尽量少做空，顺势而为");
                    break;
                    
                case MarketTrend.Sideways:
                    suggestions.Add("⚖️ 震荡行情，高抛低吸策略");
                    suggestions.Add("⚖️ 以主流币为主，控制仓位");
                    suggestions.Add("⚖️ 等待方向明确后再加仓");
                    suggestions.Add("⚠️ 避免追涨杀跌");
                    break;
                    
                case MarketTrend.Bearish:
                    suggestions.Add("⚠️ 减仓观望，降低风险");
                    suggestions.Add("⚠️ 可少量做空顺势而为");
                    suggestions.Add("⚠️ 持有优质主流币，减少山寨币");
                    suggestions.Add("💡 等待熊市底部信号");
                    break;
                    
                case MarketTrend.StrongBearish:
                    suggestions.Add("⚠️ 空仓观望为主，保护本金");
                    suggestions.Add("💡 主流币见底时，敢于低吸");
                    suggestions.Add("💡 底部建仓，长期持有（目标10倍）");
                    suggestions.Add("💡 先从主流币开始涨，再是山寨币");
                    break;
            }
            
            return suggestions;
        }
        
        /// <summary>
        /// 获取量比排行TOP20（成交额/流通市值）
        /// </summary>
        private List<VolumeRatioItem> GetVolumeRatioTop20(List<PriceStatistics> tickers)
        {
            var volumeRatioList = new List<VolumeRatioItem>();
            
            try
            {
                // 检查合约信息缓存是否已加载
                _logger.LogInformation($"🔍 检查合约信息缓存状态: IsCacheLoaded={_contractInfoService.IsCacheLoaded}, CachedCount={_contractInfoService.CachedContractCount}");
                Console.WriteLine($"🔍 检查合约信息缓存状态: IsCacheLoaded={_contractInfoService.IsCacheLoaded}, CachedCount={_contractInfoService.CachedContractCount}");
                
                if (!_contractInfoService.IsCacheLoaded)
                {
                    _logger.LogWarning("⚠️ 合约信息缓存未加载，无法计算量比排行");
                    Console.WriteLine("⚠️ 合约信息缓存未加载，无法计算量比排行");
                    return volumeRatioList;
                }
                
                _logger.LogInformation($"📊 开始计算量比排行，ticker数量: {tickers.Count}");
                Console.WriteLine($"📊 开始计算量比排行，ticker数量: {tickers.Count}");
                
                int processedCount = 0;
                int hasMarketCapCount = 0;
                
                foreach (var ticker in tickers)
                {
                    processedCount++;
                    
                    // 获取流通市值（流通数量 × 当前价格）
                    var marketCap = _contractInfoService.GetCirculatingMarketCap(ticker.Symbol, ticker.LastPrice);
                    
                    // 如果没有流通市值数据或流通市值为0，则跳过
                    if (!marketCap.HasValue || marketCap.Value <= 0)
                    {
                        if (processedCount <= 5) // 仅输出前5个作为示例
                        {
                            Console.WriteLine($"  ⏭️ {ticker.Symbol}: 无流通市值数据，跳过");
                        }
                        continue;
                    }
                    
                    hasMarketCapCount++;
                    
                    // 计算量比（成交额 / 流通市值）
                    var volumeRatio = ticker.QuoteVolume / marketCap.Value;
                    
                    volumeRatioList.Add(new VolumeRatioItem
                    {
                        Symbol = ticker.Symbol,
                        QuoteVolume = ticker.QuoteVolume,
                        CirculatingMarketCap = marketCap.Value,
                        VolumeRatio = volumeRatio,
                        CurrentPrice = ticker.LastPrice,
                        PriceChangePercent = ticker.PriceChangePercent
                    });
                    
                    if (hasMarketCapCount <= 3) // 仅输出前3个作为示例
                    {
                        Console.WriteLine($"  ✅ {ticker.Symbol}: 流通市值={marketCap.Value:N0}, 量比={volumeRatio:F4}");
                    }
                }
                
                Console.WriteLine($"📈 处理完成: 总数={processedCount}, 有市值数据={hasMarketCapCount}");
                
                // 按量比降序排序，取前20
                var top20 = volumeRatioList
                    .OrderByDescending(item => item.VolumeRatio)
                    .Take(20)
                    .ToList();
                
                _logger.LogInformation($"✅ 量比排行计算完成，有效数据: {volumeRatioList.Count} 个，TOP20: {top20.Count} 个");
                Console.WriteLine($"✅ 量比排行计算完成，有效数据: {volumeRatioList.Count} 个，TOP20: {top20.Count} 个");
                
                if (top20.Count > 0)
                {
                    Console.WriteLine($"🏆 TOP1: {top20[0].Symbol}, 量比={top20[0].VolumeRatio:F4}");
                }
                
                return top20;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算量比排行时发生错误");
                return volumeRatioList;
            }
        }
        
        /// <summary>
        /// 获取30天从最低价涨幅TOP20
        /// </summary>
        private async Task<List<PriceChangeFrom30DayLowItem>> Get30DayLowGainsTop20Async(List<PriceStatistics> tickers)
        {
            var gainsList = new List<PriceChangeFrom30DayLowItem>();
            
            try
            {
                _logger.LogInformation($"📊 开始计算30天从最低价涨幅TOP20，ticker数量: {tickers.Count}");
                
                foreach (var ticker in tickers)
                {
                    try
                    {
                        // 加载K线数据
                        var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(ticker.Symbol);
                        if (!loadSuccess || klines == null || klines.Count == 0) continue;
                        
                        // 获取过去30天的K线
                        var endDate = DateTime.UtcNow.Date.AddDays(1);
                        var startDate = endDate.AddDays(-30);
                        
                        var last30DaysKlines = klines
                            .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date < endDate.Date)
                            .ToList();
                        
                        if (last30DaysKlines.Count == 0) continue;
                        
                        // 获取30天最低价和最高价
                        var low30Day = last30DaysKlines.Min(k => k.LowPrice);
                        var high30Day = last30DaysKlines.Max(k => k.HighPrice);
                        
                        // 计算涨幅（相对最低价）
                        if (low30Day > 0)
                        {
                            var gainPercent = ((ticker.LastPrice - low30Day) / low30Day) * 100;
                            
                            // 只记录涨幅大于0的
                            if (gainPercent > 0)
                            {
                                // 计算跌幅（相对最高价）
                                var fallFromHighPercent = high30Day > 0 
                                    ? ((ticker.LastPrice - high30Day) / high30Day) * 100 
                                    : 0;
                                
                                gainsList.Add(new PriceChangeFrom30DayLowItem
                                {
                                    Symbol = ticker.Symbol,
                                    Low30Day = low30Day,
                                    High30Day = high30Day,
                                    CurrentPrice = ticker.LastPrice,
                                    GainPercent = gainPercent,
                                    FallFromHighPercent = fallFromHighPercent
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"处理 {ticker.Symbol} 的30天涨幅时出错: {ex.Message}");
                    }
                }
                
                // 按涨幅降序排序，取前20
                var top20 = gainsList
                    .OrderByDescending(item => item.GainPercent)
                    .Take(20)
                    .ToList();
                
                _logger.LogInformation($"✅ 30天从最低价涨幅TOP20计算完成，有效数据: {gainsList.Count} 个");
                
                return top20;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算30天从最低价涨幅TOP20时发生错误");
                return gainsList;
            }
        }
        
        /// <summary>
        /// 获取30天从最高价跌幅TOP20
        /// </summary>
        private async Task<List<PriceChangeFrom30DayHighItem>> Get30DayHighFallsTop20Async(List<PriceStatistics> tickers)
        {
            var fallsList = new List<PriceChangeFrom30DayHighItem>();
            
            try
            {
                _logger.LogInformation($"📊 开始计算30天从最高价跌幅TOP20，ticker数量: {tickers.Count}");
                
                foreach (var ticker in tickers)
                {
                    try
                    {
                        // 加载K线数据
                        var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(ticker.Symbol);
                        if (!loadSuccess || klines == null || klines.Count == 0) continue;
                        
                        // 获取过去30天的K线
                        var endDate = DateTime.UtcNow.Date.AddDays(1);
                        var startDate = endDate.AddDays(-30);
                        
                        var last30DaysKlines = klines
                            .Where(k => k.OpenTime.Date >= startDate.Date && k.OpenTime.Date < endDate.Date)
                            .ToList();
                        
                        if (last30DaysKlines.Count == 0) continue;
                        
                        // 获取30天最低价和最高价
                        var low30Day = last30DaysKlines.Min(k => k.LowPrice);
                        var high30Day = last30DaysKlines.Max(k => k.HighPrice);
                        
                        // 计算跌幅（相对最高价）
                        if (high30Day > 0)
                        {
                            var fallPercent = ((ticker.LastPrice - high30Day) / high30Day) * 100;
                            
                            // 只记录跌幅小于0的
                            if (fallPercent < 0)
                            {
                                // 计算涨幅（相对最低价）
                                var gainFromLowPercent = low30Day > 0 
                                    ? ((ticker.LastPrice - low30Day) / low30Day) * 100 
                                    : 0;
                                
                                fallsList.Add(new PriceChangeFrom30DayHighItem
                                {
                                    Symbol = ticker.Symbol,
                                    Low30Day = low30Day,
                                    High30Day = high30Day,
                                    CurrentPrice = ticker.LastPrice,
                                    FallPercent = fallPercent,
                                    GainFromLowPercent = gainFromLowPercent
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"处理 {ticker.Symbol} 的30天跌幅时出错: {ex.Message}");
                    }
                }
                
                // 按跌幅绝对值降序排序（跌幅最大的在前），取前20
                var top20 = fallsList
                    .OrderBy(item => item.FallPercent) // 跌幅是负数，所以升序排序就是跌幅最大的在前
                    .Take(20)
                    .ToList();
                
                _logger.LogInformation($"✅ 30天从最高价跌幅TOP20计算完成，有效数据: {fallsList.Count} 个");
                
                return top20;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算30天从最高价跌幅TOP20时发生错误");
                return fallsList;
            }
        }
    }
} 