using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 均线距离分析服务
    /// </summary>
    public class MaDistanceService
    {
        private readonly ILogger<MaDistanceService> _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly ContractInfoService _contractInfoService;
        private readonly TickerCacheService _tickerCacheService;
        private readonly string _dataDirectory;
        
        public MaDistanceService(
            ILogger<MaDistanceService> logger, 
            IBinanceSimulatedApiClient apiClient, 
            KlineDataStorageService klineStorageService,
            ContractInfoService contractInfoService,
            TickerCacheService tickerCacheService)
        {
            _logger = logger;
            _apiClient = apiClient;
            _klineStorageService = klineStorageService;
            _contractInfoService = contractInfoService;
            _tickerCacheService = tickerCacheService;
            _dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MaDistanceData");
            
            // 确保数据目录存在
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }
        
        /// <summary>
        /// 计算指定日期的均线距离分析
        /// </summary>
        public async Task<MaDistanceAnalysisResult> CalculateMaDistanceAsync(
            DateTime date, 
            int period, 
            decimal thresholdPercent)
        {
            _logger.LogInformation($"========== 开始计算均线距离分析 ==========");
            _logger.LogInformation($"参数: 日期={date:yyyy-MM-dd}, 周期={period}, 阈值={thresholdPercent}%");
            Console.WriteLine($"\n📊 ========== 均线距离计算 ==========");
            Console.WriteLine($"📅 日期: {date:yyyy-MM-dd}");
            Console.WriteLine($"📈 周期: {period}天");
            Console.WriteLine($"🎯 阈值: {thresholdPercent}%");
            
            // 1. 获取所有合约列表（使用ticker，约503个）
            var tickers = await _tickerCacheService.GetAllTickersAsync();
            var result = new MaDistanceAnalysisResult
            {
                Date = date.Date,
                Period = period,
                ThresholdPercent = thresholdPercent
            };
            
            _logger.LogInformation($"获取到 {tickers.Count} 个合约，开始计算...");
            Console.WriteLine($"✅ 获取到 {tickers.Count} 个ticker数据\n");
            
            int successCount = 0;
            int noDataCount = 0;
            int errorCount = 0;
            int testOutputCount = 0;
            
            // 2. 为每个合约计算均线距离
            foreach (var ticker in tickers)
            {
                try
                {
                    // ✅ 修正：传入ticker对象，而不是单独的参数
                    var maData = await CalculateSymbolMaDistanceAsync(
                        ticker.Symbol, 
                        date, 
                        period, 
                        thresholdPercent, 
                        ticker); // 传入整个ticker对象
                    if (maData != null)
                    {
                        result.AllData.Add(maData);
                        successCount++;
                        
                        // 前3个成功的输出详细信息
                        if (testOutputCount < 3)
                        {
                            Console.WriteLine($"✅ {ticker.Symbol}: 成功");
                            Console.WriteLine($"   当前价: {maData.CurrentPrice:F4}, 均线: {maData.MovingAverage:F4}, 距离: {maData.DistancePercent:F2}%");
                            testOutputCount++;
                        }
                    }
                    else
                    {
                        noDataCount++;
                        // 前3个失败的输出原因
                        if (noDataCount <= 3)
                        {
                            Console.WriteLine($"❌ {ticker.Symbol}: 返回null (K线数据不足或加载失败)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (errorCount <= 3)
                    {
                        Console.WriteLine($"⚠️ {ticker.Symbol}: 异常 - {ex.Message}");
                        _logger.LogError(ex, $"计算 {ticker.Symbol} 时发生异常");
                    }
                }
            }
            
            _logger.LogInformation($"计算统计: 成功={successCount}, 无数据={noDataCount}, 错误={errorCount}, 总合约={tickers.Count}");
            Console.WriteLine($"\n📊 计算统计:");
            Console.WriteLine($"   ✅ 成功: {successCount}");
            Console.WriteLine($"   ❌ 无数据: {noDataCount}");
            Console.WriteLine($"   ⚠️ 错误: {errorCount}");
            Console.WriteLine($"   📦 总数: {tickers.Count}\n");
            
            // 3. 分类到四个象限
            foreach (var data in result.AllData)
            {
                if (data.DistancePercent > 0)
                {
                    // 高于均线
                    if (data.DistancePercent <= thresholdPercent)
                    {
                        data.Zone = MaDistanceZone.AboveNear;
                        result.AboveNear.Add(data);
                    }
                    else
                    {
                        data.Zone = MaDistanceZone.AboveFar;
                        result.AboveFar.Add(data);
                    }
                }
                else
                {
                    // 低于均线
                    if (data.DistancePercent >= -thresholdPercent)
                    {
                        data.Zone = MaDistanceZone.BelowNear;
                        result.BelowNear.Add(data);
                    }
                    else
                    {
                        data.Zone = MaDistanceZone.BelowFar;
                        result.BelowFar.Add(data);
                    }
                }
            }
            
            _logger.LogInformation($"计算完成: 上近={result.AboveNear.Count}, 上远={result.AboveFar.Count}, " +
                $"下近={result.BelowNear.Count}, 下远={result.BelowFar.Count}");
            
            return result;
        }
        
        /// <summary>
        /// 计算单个合约的均线距离
        /// </summary>
        private async Task<MaDistanceData?> CalculateSymbolMaDistanceAsync(
            string symbol, 
            DateTime date, 
            int period, 
            decimal thresholdPercent,
            PriceStatistics ticker)
        {
            // 1. 从本地加载K线数据
            var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(symbol);
            
            if (!loadSuccess)
            {
                // 第一个失败的合约输出详细信息
                if (symbol == "BTCUSDT" || symbol == "ETHUSDT")
                {
                    Console.WriteLine($"⚠️ {symbol}: K线加载失败 - {loadError}");
                }
                return null;
            }
            
            if (klines == null || klines.Count == 0)
            {
                if (symbol == "BTCUSDT" || symbol == "ETHUSDT")
                {
                    Console.WriteLine($"⚠️ {symbol}: K线数据为空");
                }
                return null;
            }
            
            if (klines.Count < period)
            {
                if (symbol == "BTCUSDT" || symbol == "ETHUSDT")
                {
                    Console.WriteLine($"⚠️ {symbol}: K线总数不足 (需要{period}天，总共{klines.Count}天)");
                }
                return null;
            }
            
            // 2. 确定数据范围
            // ✅ 重要修正：
            // - 如果计算今天的数据，使用【昨天及之前的N天K线】计算均线，然后用ticker当前价计算距离
            // - 如果计算历史某天，使用【那天及之前的N天K线】
            DateTime calculationDate;
            decimal currentPrice;
            bool isToday = date.Date == DateTime.Today;
            
            if (isToday)
            {
                // 今天：使用ticker的最新价（实时价格）
                currentPrice = ticker.LastPrice;
                
                // 使用截止到昨天的K线来计算均线
                calculationDate = DateTime.Today.AddDays(-1);
                
                if (symbol == "BTCUSDT")
                {
                    Console.WriteLine($"🔍 {symbol} 调试:");
                    Console.WriteLine($"   计算今天的数据，使用昨天及之前的K线");
                    Console.WriteLine($"   计算日期: {calculationDate:yyyy-MM-dd}");
                    Console.WriteLine($"   Ticker最新价: {ticker.LastPrice:F4}");
                    Console.WriteLine($"   K线总数: {klines.Count}");
                }
            }
            else
            {
                // 历史：使用那一天的收盘价
                calculationDate = date.Date;
                currentPrice = 0; // 稍后从K线获取
            }
            
            // 3. 根据计算日期过滤K线（取该日期及之前的N天）
            var endDate = calculationDate.Date.AddDays(1); // 不包含次日0点
            var startDate = calculationDate.Date.AddDays(1 - period); // 前N天，包含计算日期
            
            if (symbol == "BTCUSDT")
            {
                Console.WriteLine($"   日期范围: {startDate:yyyy-MM-dd} 到 {endDate:yyyy-MM-dd} (不含)");
                Console.WriteLine($"   需要K线数量: {period}天");
            }
            
            var relevantKlines = klines
                .Where(k => k.OpenTime >= startDate && k.OpenTime < endDate)
                .OrderBy(k => k.OpenTime)
                .ToList();
            
            if (symbol == "BTCUSDT")
            {
                Console.WriteLine($"   筛选后K线数量: {relevantKlines.Count}");
                if (relevantKlines.Count > 0)
                {
                    Console.WriteLine($"   第一根K线: {relevantKlines.First().OpenTime:yyyy-MM-dd}");
                    Console.WriteLine($"   最后一根K线: {relevantKlines.Last().OpenTime:yyyy-MM-dd}");
                }
            }
            
            if (relevantKlines.Count < period)
            {
                if (symbol == "BTCUSDT" || symbol == "ETHUSDT")
                {
                    Console.WriteLine($"❌ {symbol}: 筛选后K线数据不足 (需要{period}天，实际{relevantKlines.Count}天)");
                }
                _logger.LogDebug($"{symbol}: K线数据不足 (需要{period}天，实际{relevantKlines.Count}天)");
                return null;
            }
            
            // 4. 计算N天移动平均（使用收盘价）
            var closePrices = relevantKlines.Select(k => k.ClosePrice).ToList();
            var movingAverage = closePrices.Average();
            
            // 5. 获取当前价和成交额
            decimal actualQuoteVolume;
            
            if (isToday)
            {
                // 今天：使用ticker的实时数据
                currentPrice = ticker.LastPrice; // ticker的最新价
                actualQuoteVolume = ticker.QuoteVolume; // ticker的24H实时成交额
            }
            else
            {
                // 历史：使用K线中对应日期的收盘价和成交额
                var latestKline = relevantKlines.Last();
                currentPrice = latestKline.ClosePrice;
                actualQuoteVolume = latestKline.QuoteVolume;
            }
            
            // 6. 计算距离百分比：(当前价 - 均线) / 均线 * 100%
            var distancePercent = movingAverage != 0 
                ? ((currentPrice - movingAverage) / movingAverage) * 100m 
                : 0m;
            
            // 7. 计算流通市值和量比
            decimal? circulatingMarketCap = null;
            decimal? volumeRatio = null;
            
            var contractInfo = _contractInfoService.GetContractInfo(symbol);
            if (contractInfo != null && contractInfo.CirculatingSupply > 0)
            {
                // 计算流通市值 = 流通数量 × 当前价格
                circulatingMarketCap = contractInfo.CirculatingSupply * currentPrice;
                
                // 计算量比 = 24H成交额 / 流通市值（处理除0异常）
                if (circulatingMarketCap > 0)
                {
                    volumeRatio = actualQuoteVolume / circulatingMarketCap.Value;
                }
                else
                {
                    volumeRatio = 0; // 流通市值为0时，量比归零
                }
            }
            
            return new MaDistanceData
            {
                Symbol = symbol,
                CurrentPrice = currentPrice,
                PriceChangePercent = ticker.PriceChangePercent, // ✅ 使用ticker的涨跌幅
                QuoteVolume = actualQuoteVolume, // ✅ 使用实际成交额
                CirculatingMarketCap = circulatingMarketCap, // 流通市值
                VolumeRatio = volumeRatio, // 量比
                MovingAverage = movingAverage,
                DistancePercent = distancePercent
            };
        }
        
        /// <summary>
        /// 保存分析结果到本地
        /// </summary>
        public async Task SaveAnalysisResultAsync(MaDistanceAnalysisResult result)
        {
            var fileName = GetHistoryFileName(result.Period, result.ThresholdPercent);
            var filePath = Path.Combine(_dataDirectory, fileName);
            
            // 加载现有数据
            var historyFile = await LoadHistoryFileAsync(result.Period, result.ThresholdPercent);
            
            // 添加/更新当日数据
            var dateKey = result.Date.ToString("yyyy-MM-dd");
            historyFile.DailyDistributions[dateKey] = result.GetDistribution();
            historyFile.LastUpdated = DateTime.UtcNow;
            
            // 保存到文件
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            var json = JsonSerializer.Serialize(historyFile, options);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInformation($"保存分析结果成功: {filePath}");
        }
        
        /// <summary>
        /// 加载历史数据文件
        /// </summary>
        public async Task<MaDistanceHistoryFile> LoadHistoryFileAsync(int period, decimal thresholdPercent)
        {
            var fileName = GetHistoryFileName(period, thresholdPercent);
            var filePath = Path.Combine(_dataDirectory, fileName);
            
            if (!File.Exists(filePath))
            {
                return new MaDistanceHistoryFile
                {
                    Period = period,
                    ThresholdPercent = thresholdPercent
                };
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var historyFile = JsonSerializer.Deserialize<MaDistanceHistoryFile>(json);
                return historyFile ?? new MaDistanceHistoryFile
                {
                    Period = period,
                    ThresholdPercent = thresholdPercent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"加载历史文件失败: {filePath}");
                return new MaDistanceHistoryFile
                {
                    Period = period,
                    ThresholdPercent = thresholdPercent
                };
            }
        }
        
        /// <summary>
        /// 获取历史分布数据（按日期倒序）
        /// </summary>
        public async Task<List<DailyMaDistribution>> GetHistoryDistributionsAsync(
            int period, 
            decimal thresholdPercent, 
            int days = 30)
        {
            var historyFile = await LoadHistoryFileAsync(period, thresholdPercent);
            
            return historyFile.DailyDistributions.Values
                .OrderByDescending(d => d.Date)
                .Take(days)
                .ToList();
        }
        
        /// <summary>
        /// 获取历史文件名
        /// </summary>
        private string GetHistoryFileName(int period, decimal thresholdPercent)
        {
            return $"ma_distance_p{period}_t{thresholdPercent:F1}.json";
        }
        
        /// <summary>
        /// 检查是否已有指定日期的数据
        /// </summary>
        public async Task<bool> HasDataForDateAsync(DateTime date, int period, decimal thresholdPercent)
        {
            var historyFile = await LoadHistoryFileAsync(period, thresholdPercent);
            var dateKey = date.ToString("yyyy-MM-dd");
            return historyFile.DailyDistributions.ContainsKey(dateKey);
        }
    }
} 