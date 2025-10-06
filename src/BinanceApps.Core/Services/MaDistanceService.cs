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
        private readonly string _dataDirectory;
        
        public MaDistanceService(ILogger<MaDistanceService> logger, IBinanceSimulatedApiClient apiClient, KlineDataStorageService klineStorageService)
        {
            _logger = logger;
            _apiClient = apiClient;
            _klineStorageService = klineStorageService;
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
            _logger.LogInformation($"开始计算均线距离分析: 日期={date:yyyy-MM-dd}, 周期={period}, 阈值={thresholdPercent}%");
            
            // 1. 获取所有合约列表（使用ticker，约503个）
            var tickers = await _apiClient.GetAllTicksAsync();
            var result = new MaDistanceAnalysisResult
            {
                Date = date.Date,
                Period = period,
                ThresholdPercent = thresholdPercent
            };
            
            _logger.LogInformation($"获取到 {tickers.Count} 个合约，开始计算...");
            
            int successCount = 0;
            int noDataCount = 0;
            int errorCount = 0;
            
            // 2. 为每个合约计算均线距离
            foreach (var ticker in tickers)
            {
                try
                {
                    var maData = await CalculateSymbolMaDistanceAsync(ticker.Symbol, date, period, thresholdPercent, ticker.PriceChangePercent, ticker.QuoteVolume);
                    if (maData != null)
                    {
                        result.AllData.Add(maData);
                        successCount++;
                    }
                    else
                    {
                        noDataCount++;
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogDebug($"计算 {ticker.Symbol} 失败: {ex.Message}");
                }
            }
            
            _logger.LogInformation($"计算统计: 成功={successCount}, 无数据={noDataCount}, 错误={errorCount}, 总合约={tickers.Count}");
            
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
            decimal priceChangePercent,
            decimal quoteVolume)
        {
            // 1. 从本地加载K线数据
            var (klines, loadSuccess, loadError) = await _klineStorageService.LoadKlineDataAsync(symbol);
            
            if (!loadSuccess || klines == null || klines.Count < period)
            {
                return null;
            }
            
            // 2. 根据指定日期过滤K线（取该日期前N天，包含当天）
            var endDate = date.Date.AddDays(1); // 不包含次日0点
            var startDate = date.Date.AddDays(1 - period); // 前N天，包含当天
            
            var relevantKlines = klines
                .Where(k => k.OpenTime >= startDate && k.OpenTime < endDate)
                .OrderBy(k => k.OpenTime)
                .ToList();
            
            if (relevantKlines.Count < period)
            {
                return null;
            }
            
            // 3. 计算N天移动平均（使用收盘价）
            var closePrices = relevantKlines.Select(k => k.ClosePrice).ToList();
            var movingAverage = closePrices.Average();
            
            // 4. 获取最后一天的收盘价（当前价）和成交额
            var latestKline = relevantKlines.Last();
            var currentPrice = latestKline.ClosePrice;
            
            // ✅ 使用K线中对应日期的实际成交额（而不是实时ticker数据）
            var actualQuoteVolume = latestKline.QuoteVolume;
            
            // 5. 计算距离百分比：(当前价 - 均线) / 均线 * 100%
            var distancePercent = movingAverage != 0 
                ? ((currentPrice - movingAverage) / movingAverage) * 100m 
                : 0m;
            
            return new MaDistanceData
            {
                Symbol = symbol,
                CurrentPrice = currentPrice,
                PriceChangePercent = priceChangePercent, // 使用传入的参数
                QuoteVolume = actualQuoteVolume, // ✅ 使用历史当日的实际成交额
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