using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 量比异动选股服务
    /// </summary>
    public class VolumeRatioService : IVolumeRatioService
    {
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly ContractInfoService _contractInfoService;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly SymbolInfoCacheService _symbolInfoCacheService;
        private readonly ILogger<VolumeRatioService>? _logger;

        public VolumeRatioService(
            IBinanceSimulatedApiClient apiClient,
            ContractInfoService contractInfoService,
            KlineDataStorageService klineStorageService,
            SymbolInfoCacheService symbolInfoCacheService,
            ILogger<VolumeRatioService>? logger = null)
        {
            _apiClient = apiClient;
            _contractInfoService = contractInfoService;
            _klineStorageService = klineStorageService;
            _symbolInfoCacheService = symbolInfoCacheService;
            _logger = logger;
        }

        /// <summary>
        /// 执行量比异动选股
        /// </summary>
        public async Task<List<VolumeRatioResult>> SearchVolumeRatioAsync(VolumeRatioFilter filter)
        {
            try
            {
                _logger?.LogInformation("开始执行量比异动选股");
                Console.WriteLine("🔍 开始执行量比异动选股...");

                // 步骤1：获取所有合约的24H数据
                Console.WriteLine("步骤1：获取ticker数据...");
                var allTicks = await _apiClient.GetAllTicksAsync();
                if (allTicks == null || !allTicks.Any())
                {
                    Console.WriteLine("❌ 无法获取真实24H数据，网络不可用");
                    Console.WriteLine("❌ 量比异动选股失败：网络连接异常，请检查网络连接后重试");
                    _logger?.LogError("无法获取ticker数据，网络连接失败");
                    return new List<VolumeRatioResult>();
                }

                Console.WriteLine($"✅ 步骤1完成：ticker数据已经一次性获得，共 {allTicks.Count} 个合约");

                // 步骤1.5：过滤下架合约
                Console.WriteLine("步骤1.5：过滤下架合约...");
                var allSymbols = await _symbolInfoCacheService.GetAllSymbolsInfoAsync();
                var tradingSymbols = new HashSet<string>();
                
                if (allSymbols != null && allSymbols.Count > 0)
                {
                    tradingSymbols = allSymbols
                        .Where(s => s.IsTrading && s.QuoteAsset == "USDT" && s.ContractType == ContractType.Perpetual)
                        .Select(s => s.Symbol)
                        .ToHashSet();
                    Console.WriteLine($"📊 找到 {tradingSymbols.Count} 个可交易的USDT永续合约");
                }
                
                // 过滤ticker数据，只保留可交易的合约
                var originalTickCount = allTicks.Count;
                allTicks = allTicks.Where(t => tradingSymbols.Contains(t.Symbol)).ToList();
                var filteredTickCount = originalTickCount - allTicks.Count;
                Console.WriteLine($"✅ 步骤1.5完成：过滤掉 {filteredTickCount} 个下架或不可交易合约，剩余 {allTicks.Count} 个");

                // 步骤2：获取所有合约的流通量数据
                Console.WriteLine("步骤2：获取流通量、总发行量数据...");
                // 从ContractInfoService获取缓存数据
                var contractCache = _contractInfoService.GetAllContractInfo();
                Console.WriteLine($"📊 缓存状态：{contractCache.Count} 个合约");
                Console.WriteLine($"✅ 步骤2完成：流通量、总发行量数据已经成功获得，共 {contractCache.Count} 个合约");

                // 步骤3：计算全部合约市值
                Console.WriteLine("步骤3：计算全部合约市值...");
                var marketCapResults = new List<(PriceStatistics tick, decimal circulatingMarketCap, decimal totalMarketCap, decimal circulatingRatio, decimal volumeRatio)>();
                var supplyDataCount = 0;
                var marketCapCalculated = 0;

                foreach (var tick in allTicks)
                {
                    try
                    {
                        // 获取合约信息
                        var contractInfo = _contractInfoService.GetContractInfo(tick.Symbol);
                        if (contractInfo == null)
                        {
                            continue; // 跳过没有合约信息的合约
                        }

                        supplyDataCount++;

                        // 计算流通市值
                        var circulatingMarketCap = tick.LastPrice * contractInfo.CirculatingSupply;
                        var totalMarketCap = tick.LastPrice * contractInfo.TotalSupply;
                        var circulatingRatio = contractInfo.TotalSupply > 0 ? contractInfo.CirculatingSupply / contractInfo.TotalSupply : 0;

                        // 计算量比
                        var volumeRatio = tick.QuoteVolume / circulatingMarketCap;
                        if (volumeRatio <= 0)
                        {
                            continue;
                        }

                        marketCapCalculated++;

                        // 存储市值计算结果
                        marketCapResults.Add((tick, circulatingMarketCap, totalMarketCap, circulatingRatio, volumeRatio));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"处理合约 {tick.Symbol} 时出错");
                        continue;
                    }
                }

                Console.WriteLine($"✅ 步骤3完成：全部合约市值已经计算完成，共 {marketCapCalculated} 个合约");

                // 步骤4：根据市值筛选
                Console.WriteLine("步骤4：根据市值筛选...");
                var marketCapFiltered = new List<(PriceStatistics tick, decimal circulatingMarketCap, decimal totalMarketCap, decimal circulatingRatio, decimal volumeRatio)>();
                foreach (var item in marketCapResults)
                {
                    if (PassesMarketCapFilter(item.tick, item.circulatingMarketCap, item.totalMarketCap, filter))
                    {
                        marketCapFiltered.Add(item);
                    }
                }
                Console.WriteLine($"✅ 步骤4完成：根据市值筛选出 {marketCapFiltered.Count} 个合约");

                // 步骤5：根据成交额筛选
                Console.WriteLine("步骤5：根据成交额筛选...");
                var volumeFiltered = new List<(PriceStatistics tick, decimal circulatingMarketCap, decimal totalMarketCap, decimal circulatingRatio, decimal volumeRatio)>();
                foreach (var item in marketCapFiltered)
                {
                    if (PassesVolumeFilter(item.tick, filter))
                    {
                        volumeFiltered.Add(item);
                    }
                }
                Console.WriteLine($"✅ 步骤5完成：根据成交额进一步筛选出 {volumeFiltered.Count} 个合约");

                // 步骤6：量比计算和筛选
                Console.WriteLine("步骤6：量比计算和筛选...");
                var volumeRatioFiltered = new List<(PriceStatistics tick, decimal circulatingMarketCap, decimal totalMarketCap, decimal circulatingRatio, decimal volumeRatio)>();
                foreach (var item in volumeFiltered)
                {
                    if (PassesVolumeRatioFilter(item.volumeRatio, filter))
                    {
                        volumeRatioFiltered.Add(item);
                    }
                }
                Console.WriteLine($"✅ 步骤6完成：量比计算完成，并根据量比范围筛选出 {volumeRatioFiltered.Count} 个合约");

                // 步骤7：获取K线数据并计算均线距离
                Console.WriteLine($"步骤7：获取 {volumeRatioFiltered.Count} 个合约的26根小时K线...");
                var results = new List<VolumeRatioResult>();
                var klineProcessed = 0;
                var maDistanceCalculated = 0;

                foreach (var item in volumeRatioFiltered)
                {
                    try
                    {
                        // 获取均线距离和同侧K线数
                        var (maDistance, sameSideCloseCount, sameSideExtremeCount, maPrice) = await GetMaDistanceAndSameSideCountAsync(item.tick.Symbol, item.tick.LastPrice, filter.MaPeriod);
                        klineProcessed++;
                        
                        if (maDistance == null)
                        {
                            continue;
                        }

                        maDistanceCalculated++;

                        // 创建结果（金额转换为万为单位）
                        var result = new VolumeRatioResult
                        {
                            Symbol = item.tick.Symbol,
                            PriceChangePercent = item.tick.PriceChangePercent,
                            Volume24H = item.tick.QuoteVolume / 10000, // 转换为万
                            CirculatingMarketCap = item.circulatingMarketCap / 10000, // 转换为万
                            TotalMarketCap = item.totalMarketCap / 10000, // 转换为万
                            CirculatingRatio = item.circulatingRatio,
                            VolumeRatio = item.volumeRatio,
                            MaDistancePercent = maDistance.Value,
                            LastPrice = item.tick.LastPrice,
                            Ma26Price = maPrice,
                            CirculatingSupply = _contractInfoService.GetContractInfo(item.tick.Symbol)?.CirculatingSupply ?? 0,
                            TotalSupply = _contractInfoService.GetContractInfo(item.tick.Symbol)?.TotalSupply ?? 0,
                            SameSideCloseCount = sameSideCloseCount,
                            SameSideExtremeCount = sameSideExtremeCount
                        };

                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"处理合约 {item.tick.Symbol} 的K线数据时出错");
                        continue;
                    }
                }

                Console.WriteLine($"✅ 步骤7完成：获取 {klineProcessed} 个合约的26根小时K线，计算均线，并用最新价计算距离完成");
                Console.WriteLine($"✅ 步骤8完成：显示所有 {results.Count} 个合约的均线距离");
                Console.WriteLine("🎉 工作完成！");

                Console.WriteLine($"✅ 量比异动选股完成，找到 {results.Count} 个符合条件的合约");
                return results;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "量比异动选股执行失败");
                Console.WriteLine($"❌ 量比异动选股执行失败: {ex.Message}");
                return new List<VolumeRatioResult>();
            }
        }

        /// <summary>
        /// 获取合约的26小时均线距离百分比
        /// </summary>
        public async Task<decimal?> Get26HourMaAsync(string symbol)
        {
            try
            {
                var (klines, success, errorMessage) = await _klineStorageService.LoadKlineDataAsync(symbol);
                if (!success || klines == null || klines.Count < 26)
                {
                    return null;
                }

                // 获取最近26个小时的K线数据
                var recentKlines = klines
                    .OrderByDescending(k => k.OpenTime)
                    .Take(26)
                    .ToList();

                if (recentKlines.Count < 26)
                {
                    return null;
                }

                // 计算26小时均线
                var ma26 = recentKlines.Average(k => k.ClosePrice);
                var latestPrice = recentKlines.First().ClosePrice;

                // 计算距离百分比
                var distancePercent = (latestPrice - ma26) / ma26 * 100;
                return distancePercent;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"获取 {symbol} 的26小时均线失败");
                return null;
            }
        }

        /// <summary>
        /// 获取26小时均线距离百分比
        /// </summary>
        private async Task<(decimal? MaDistance, int SameSideCloseCount, int SameSideExtremeCount, decimal MaPrice)> GetMaDistanceAndSameSideCountAsync(string symbol, decimal currentPrice, int maPeriod)
        {
            try
            {
                // 先检查合约是否已下架
                var allSymbols = await _symbolInfoCacheService.GetAllSymbolsInfoAsync();
                if (allSymbols != null && allSymbols.Count > 0)
                {
                    var symbolInfo = allSymbols.FirstOrDefault(s => s.Symbol == symbol);
                    if (symbolInfo == null || !symbolInfo.IsTrading)
                    {
                        Console.WriteLine($"⚠️ {symbol} 合约已下架或不存在，跳过K线数据获取");
                        return (null, 0, 0, 0);
                    }
                }

                // 先打印原始K线数据信息
                Console.WriteLine($"🔍 {symbol} 开始获取K线数据...");
                var (klines, success, errorMessage) = await _klineStorageService.LoadKlineDataAsync(symbol);
                
                if (!success || klines == null)
                {
                    Console.WriteLine($"❌ {symbol} 获取K线数据失败: {errorMessage}");
                    return (null, 0, 0, 0);
                }
                
                Console.WriteLine($"📊 {symbol} 原始K线数据总数: {klines.Count}");
                
                // 检查是否为1小时K线，如果不是则强制重新下载
                bool isHourlyKline = true;
                if (klines.Count >= 2)
                {
                    var firstKline = klines.First();
                    var secondKline = klines.Skip(1).First();
                    var timeDiff = secondKline.OpenTime - firstKline.OpenTime;
                    
                    Console.WriteLine($"📊 {symbol} K线时间间隔检查:");
                    Console.WriteLine($"  第一条: {firstKline.OpenTime:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  第二条: {secondKline.OpenTime:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"  时间差: {timeDiff.TotalHours:F1} 小时");
                    
                    if (Math.Abs(timeDiff.TotalHours - 1.0) < 0.1)
                    {
                        Console.WriteLine($"✅ {symbol} 确认为1小时K线数据");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ {symbol} 不是1小时K线数据，时间间隔为 {timeDiff.TotalHours:F1} 小时");
                        Console.WriteLine($"🔄 {symbol} 强制重新下载1小时K线数据...");
                        isHourlyKline = false;
                    }
                }
                
                // 如果不是1小时K线，强制重新下载
                if (!isHourlyKline)
                {
                    Console.WriteLine($"🔄 {symbol} 开始强制重新下载1小时K线数据...");
                    
                    // 先删除旧的K线数据文件，确保重新下载
                    var filePath = _klineStorageService.GetKlineDataFilePath(symbol);
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                            Console.WriteLine($"🗑️ {symbol} 已删除旧的K线数据文件");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ {symbol} 删除旧K线数据文件失败: {ex.Message}");
                        }
                    }
                    
                    // 直接获取指定数量的1小时K线数据
                    var klinesNeeded = maPeriod + 10; // 26 + 10 = 36条K线
                    Console.WriteLine($"📊 {symbol} 直接获取 {klinesNeeded} 条1小时K线数据");
                    
                    try
                    {
                        // 直接调用API获取1小时K线数据
                        var newKlines = await _apiClient.GetKlinesAsync(symbol, KlineInterval.OneHour, klinesNeeded);
                        
                        if (newKlines != null && newKlines.Count > 0)
                        {
                            Console.WriteLine($"✅ {symbol} 直接获取到 {newKlines.Count} 条1小时K线数据");
                            
                            // 保存到本地文件
                            var (saveSuccess, saveError) = await _klineStorageService.SaveKlineDataAsync(symbol, newKlines);
                            if (saveSuccess)
                            {
                                Console.WriteLine($"✅ {symbol} 1小时K线数据保存成功");
                                // 重新加载数据
                                var (newKlines2, newSuccess, newError) = await _klineStorageService.LoadKlineDataAsync(symbol);
                                if (newSuccess && newKlines2 != null)
                                {
                                    klines = newKlines2;
                                    Console.WriteLine($"📊 {symbol} 重新加载后K线数据总数: {klines.Count}");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"❌ {symbol} 保存1小时K线数据失败");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"❌ {symbol} 直接获取1小时K线数据失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ {symbol} 直接获取1小时K线数据异常: {ex.Message}");
                    }
                }
                
                // 打印前5条和后5条K线的时间信息
                if (klines.Count > 0)
                {
                    Console.WriteLine($"📊 {symbol} 前5条K线时间:");
                    for (int i = 0; i < Math.Min(5, klines.Count); i++)
                    {
                        var kline = klines[i];
                        Console.WriteLine($"  {i+1}: {kline.OpenTime:yyyy-MM-dd HH:mm:ss} - {kline.CloseTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    if (klines.Count > 5)
                    {
                        Console.WriteLine($"📊 {symbol} 后5条K线时间:");
                        for (int i = Math.Max(0, klines.Count - 5); i < klines.Count; i++)
                        {
                            var kline = klines[i];
                            Console.WriteLine($"  {i+1}: {kline.OpenTime:yyyy-MM-dd HH:mm:ss} - {kline.CloseTime:yyyy-MM-dd HH:mm:ss}");
                        }
                    }
                }
                
                if (klines.Count < maPeriod)
                {
                    Console.WriteLine($"⚠️ {symbol} K线数据不足：需要{maPeriod}根，实际{klines.Count}根");
                    return (null, 0, 0, 0);
                }

                // 获取最近N个小时的K线数据
                var recentKlines = klines
                    .OrderByDescending(k => k.OpenTime)
                    .Take(maPeriod)
                    .ToList();

                if (recentKlines.Count < maPeriod)
                {
                    Console.WriteLine($"⚠️ {symbol} K线数据不足：需要{maPeriod}根，实际{recentKlines.Count}根");
                    return (null, 0, 0, 0);
                }

                // 详细输出计算过程
                Console.WriteLine($"📊 {symbol} 计算过程：");
                Console.WriteLine($"📊 获取到 {recentKlines.Count} 根K线数据");
                
                // 输出K线收盘价
                Console.WriteLine($"📊 {maPeriod}根K线收盘价：");
                for (int i = 0; i < recentKlines.Count; i++)
                {
                    var kline = recentKlines[i];
                    Console.WriteLine($"  K{i+1}: {kline.ClosePrice:F8} (时间: {kline.OpenTime:yyyy-MM-dd HH:mm:ss})");
                }

                // 计算N小时均线
                var maPrice = recentKlines.Average(k => k.ClosePrice);
                Console.WriteLine($"📊 {maPeriod}根K线收盘价均值: {maPrice:F8}");
                Console.WriteLine($"📊 当前价格: {currentPrice:F8}");

                // 计算距离百分比
                var distancePercent = (currentPrice - maPrice) / maPrice * 100;
                Console.WriteLine($"📊 距离百分比: {distancePercent:F4}%");

                // 计算同侧K线数
                var (sameSideCloseCount, sameSideExtremeCount) = CalculateSameSideCount(recentKlines, maPrice, distancePercent > 0);

                return (distancePercent, sameSideCloseCount, sameSideExtremeCount, maPrice);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"获取 {symbol} 的{maPeriod}小时均线距离失败");
                return (null, 0, 0, 0);
            }
        }

        /// <summary>
        /// 计算同侧K线数量
        /// </summary>
        private (int SameSideCloseCount, int SameSideExtremeCount) CalculateSameSideCount(List<Kline> klines, decimal maPrice, bool isAboveMa)
        {
            int sameSideCloseCount = 0;
            int sameSideExtremeCount = 0;

            // 从最新时间往前检索
            foreach (var kline in klines)
            {
                if (isAboveMa)
                {
                    // 距离是正数，检查收盘价是否大于均值
                    if (kline.ClosePrice > maPrice)
                    {
                        sameSideCloseCount++;
                    }
                    else
                    {
                        break; // 小于均值停止
                    }

                    // 检查最低价是否大于均值
                    if (kline.LowPrice > maPrice)
                    {
                        sameSideExtremeCount++;
                    }
                    else
                    {
                        break; // 最低价小于等于均值停止
                    }
                }
                else
                {
                    // 距离是负数，检查收盘价是否小于均值
                    if (kline.ClosePrice < maPrice)
                    {
                        sameSideCloseCount++;
                    }
                    else
                    {
                        break; // 大于均值停止
                    }

                    // 检查最高价是否小于均值
                    if (kline.HighPrice < maPrice)
                    {
                        sameSideExtremeCount++;
                    }
                    else
                    {
                        break; // 最高价大于等于均值停止
                    }
                }
            }

            return (sameSideCloseCount, sameSideExtremeCount);
        }

        /// <summary>
        /// 计算量比
        /// </summary>
        public decimal? CalculateVolumeRatio(string symbol, decimal volume24H, decimal circulatingSupply)
        {
            try
            {
                if (circulatingSupply <= 0)
                {
                    return null;
                }

                // 量比 = 24H成交额 / 流通市值
                // 这里需要获取当前价格来计算流通市值
                // 由于我们在这个方法中没有价格信息，返回null，让调用方处理
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"计算 {symbol} 的量比失败");
                return null;
            }
        }

        /// <summary>
        /// 检查是否通过市值筛选
        /// </summary>
        private bool PassesMarketCapFilter(PriceStatistics tick, decimal circulatingMarketCap, decimal totalMarketCap, VolumeRatioFilter filter)
        {
            // 检查流通市值范围（转换为万为单位）
            var circulatingMarketCapInWan = circulatingMarketCap / 10000;
            if (filter.MinMarketCap.HasValue && circulatingMarketCapInWan < filter.MinMarketCap.Value)
                return false;
            if (filter.MaxMarketCap.HasValue && circulatingMarketCapInWan > filter.MaxMarketCap.Value)
                return false;

            return true;
        }

        /// <summary>
        /// 检查是否通过成交额筛选
        /// </summary>
        private bool PassesVolumeFilter(PriceStatistics tick, VolumeRatioFilter filter)
        {
            // 检查24H成交额范围（转换为万为单位）
            var volumeInWan = tick.QuoteVolume / 10000;
            if (filter.Min24HVolume.HasValue && volumeInWan < filter.Min24HVolume.Value)
                return false;
            if (filter.Max24HVolume.HasValue && volumeInWan > filter.Max24HVolume.Value)
                return false;

            return true;
        }

        /// <summary>
        /// 检查是否通过量比筛选
        /// </summary>
        private bool PassesVolumeRatioFilter(decimal volumeRatio, VolumeRatioFilter filter)
        {
            // 检查量比范围
            if (filter.MinVolumeRatio.HasValue && volumeRatio < filter.MinVolumeRatio.Value)
                return false;
            if (filter.MaxVolumeRatio.HasValue && volumeRatio > filter.MaxVolumeRatio.Value)
                return false;

            return true;
        }

        /// <summary>
        /// 检查是否通过多空筛选
        /// </summary>
        private bool PassesLongShortFilter(decimal maDistance, VolumeRatioFilter filter)
        {
            if (filter.IsLong)
            {
                // 多头：均线上方，距离在0到设定值之间
                return maDistance >= 0 && maDistance <= filter.MaDistancePercent;
            }
            else
            {
                // 空头：均线下方，距离在-设定值到0之间
                return maDistance >= -filter.MaDistancePercent && maDistance <= 0;
            }
        }

    }
}
