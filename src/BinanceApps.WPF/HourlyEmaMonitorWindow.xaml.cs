using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using BinanceApps.Core.Interfaces;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 小时均线监控窗口
    /// </summary>
    public partial class HourlyEmaMonitorWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHourlyEmaService _hourlyEmaService;
        private readonly ContractInfoService? _contractInfoService;
        private readonly ILogger<HourlyEmaMonitorWindow>? _logger;
        private List<HourlyEmaMonitorResult> _currentResults = new List<HourlyEmaMonitorResult>();

        // 浮动监控窗口
        private FloatingMonitorWindow? _floatingMonitor = null;
        
        // 配置文件路径
        private const string ConfigFilePath = "hourly_ema_config.json";

        public HourlyEmaMonitorWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _hourlyEmaService = _serviceProvider.GetRequiredService<IHourlyEmaService>();
            _contractInfoService = _serviceProvider.GetService<ContractInfoService>();
            _logger = _serviceProvider.GetService<ILogger<HourlyEmaMonitorWindow>>();
            
            InitializeWindow();
            
            // 自动显示浮动监控窗口
            ShowFloatingMonitor();
        }

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // 初始化数据网格
                dgResults.ItemsSource = _currentResults;
                
                // 加载上次保存的配置
                LoadConfig();
                
                // 设置状态
                txtStatus.Text = "就绪";
                
                Console.WriteLine("✅ 小时均线监控窗口初始化完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "小时均线监控窗口初始化失败");
                MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取小时K线按钮点击事件
        /// </summary>
        private async void BtnFetchKlines_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取参数
                if (!int.TryParse(txtEmaPeriod.Text, out var emaPeriod) || emaPeriod <= 0)
                {
                    MessageBox.Show("请输入有效的N天均线参数（大于0的整数）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtKlineCount.Text, out var klineCount) || klineCount <= 0)
                {
                    MessageBox.Show("请输入有效的X根K线参数（大于0的整数）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (klineCount < emaPeriod)
                {
                    MessageBox.Show($"X根K线数量（{klineCount}）必须大于等于N天均线（{emaPeriod}）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 禁用按钮
                btnFetchKlines.IsEnabled = false;
                btnFetchKlines.Content = "获取中...";
                txtStatus.Text = "正在获取小时K线数据...";
                
                var parameters = new HourlyEmaParameters
                {
                    EmaPeriod = emaPeriod,
                    KlineCount = klineCount
                };

                // 获取K线数据
                var success = await _hourlyEmaService.FetchHourlyKlinesAsync(parameters, (progress) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtProgress.Text = $"进度: {progress.CompletedCount}/{progress.TotalCount} ({progress.ProgressPercent}%) - {progress.CurrentSymbol}";
                    });
                });

                if (success)
                {
                    txtStatus.Text = "K线数据获取完成，正在计算EMA...";
                    
                    // 计算EMA
                    var calculateSuccess = await _hourlyEmaService.CalculateEmaAsync(parameters);
                    
                    if (calculateSuccess)
                    {
                        // 获取监控结果并补充额外数据
                        await RefreshMonitorResultsAsync();
                        
                        txtStatus.Text = "数据准备完成";
                        txtProgress.Text = $"成功获取 {_currentResults.Count} 个合约的数据";
                        
                        // 启用相关按钮
                        btnUpdateKlines.IsEnabled = true;
                        btnCalculate.IsEnabled = true;
                        
                        MessageBox.Show($"成功获取并计算 {_currentResults.Count} 个合约的小时K线和EMA数据", 
                            "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        txtStatus.Text = "EMA计算失败";
                        MessageBox.Show("EMA计算失败，请查看日志", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    txtStatus.Text = "获取K线数据失败";
                    MessageBox.Show("获取K线数据失败，请检查网络连接和API设置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                // 恢复按钮状态
                btnFetchKlines.IsEnabled = true;
                btnFetchKlines.Content = "📥 获取小时K线";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取小时K线失败");
                MessageBox.Show($"获取小时K线失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复按钮状态
                btnFetchKlines.IsEnabled = true;
                btnFetchKlines.Content = "📥 获取小时K线";
                txtStatus.Text = "获取失败";
            }
        }

        /// <summary>
        /// 更新K线按钮点击事件
        /// </summary>
        private async void BtnUpdateKlines_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 禁用按钮
                btnUpdateKlines.IsEnabled = false;
                btnUpdateKlines.Content = "更新中...";
                txtStatus.Text = "正在增量更新K线数据...";
                
                // 增量更新K线
                var success = await _hourlyEmaService.UpdateHourlyKlinesAsync((progress) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtProgress.Text = $"更新进度: {progress.CompletedCount}/{progress.TotalCount} ({progress.ProgressPercent}%) - {progress.CurrentSymbol}";
                    });
                });

                if (success)
                {
                    txtStatus.Text = "K线更新完成，正在计算EMA...";
                    txtProgress.Text = "K线数据已更新到最新";
                    
                    // 自动重新计算EMA（使用当前参数）
                    bool emaCalculated = false;
                    if (int.TryParse(txtEmaPeriod.Text, out var emaPeriod) && emaPeriod > 0)
                    {
                        var parameters = new HourlyEmaParameters
                        {
                            EmaPeriod = emaPeriod,
                            KlineCount = int.TryParse(txtKlineCount.Text, out var klineCount) ? klineCount : 100
                        };
                        
                        Console.WriteLine("📈 自动重新计算EMA...");
                        emaCalculated = await _hourlyEmaService.CalculateEmaAsync(parameters);
                        
                        if (emaCalculated)
                        {
                            // 自动重新计算大于/小于EMA数量
                            Console.WriteLine("🔢 自动重新计算连续数量...");
                            await _hourlyEmaService.CalculateAboveBelowEmaCountsAsync();
                            
                            // 刷新显示
                            Console.WriteLine("🔍 刷新显示结果...");
                            await RefreshMonitorResultsAsync();
                            
                            txtStatus.Text = "更新并计算完成";
                            txtProgress.Text = $"已更新并重新计算，共 {_currentResults.Count} 个合约";
                            MessageBox.Show("K线数据更新并重新计算成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            txtStatus.Text = "K线更新完成，但EMA计算失败";
                            MessageBox.Show("K线数据更新成功，但EMA计算失败", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        txtStatus.Text = "K线更新完成";
                        MessageBox.Show("K线数据更新成功，请点击【计算】按钮计算EMA", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    
                    // 启用计算按钮
                    btnCalculate.IsEnabled = true;
                }
                else
                {
                    txtStatus.Text = "K线更新失败";
                    MessageBox.Show("K线数据更新失败，请查看日志", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                // 恢复按钮状态
                btnUpdateKlines.IsEnabled = true;
                btnUpdateKlines.Content = "🔄 更新K线";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "更新K线失败");
                MessageBox.Show($"更新K线失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复按钮状态
                btnUpdateKlines.IsEnabled = true;
                btnUpdateKlines.Content = "🔄 更新K线";
                txtStatus.Text = "更新失败";
            }
        }

        /// <summary>
        /// 计算按钮点击事件
        /// </summary>
        private async void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 禁用按钮
                btnCalculate.IsEnabled = false;
                btnCalculate.Content = "计算中...";
                txtStatus.Text = "正在计算连续大于/小于EMA的K线数量...";
                
                // 计算连续数量
                var success = await _hourlyEmaService.CalculateAboveBelowEmaCountsAsync();

                if (success)
                {
                    // 重新获取监控结果
                    await RefreshMonitorResultsAsync();
                    
                    txtStatus.Text = "计算完成";
                    txtProgress.Text = "连续大于/小于EMA数量计算完成";
                    MessageBox.Show("计算完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    txtStatus.Text = "计算失败";
                    MessageBox.Show("计算失败，请查看日志", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                // 恢复按钮状态
                btnCalculate.IsEnabled = true;
                btnCalculate.Content = "🔢 计算";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "计算失败");
                MessageBox.Show($"计算失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复按钮状态
                btnCalculate.IsEnabled = true;
                btnCalculate.Content = "🔢 计算";
                txtStatus.Text = "计算失败";
            }
        }

        /// <summary>
        /// 筛选按钮点击事件
        /// </summary>
        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filter = new HourlyEmaFilter();
                bool hasFilter = false;
                
                // 解析大于EMA数量
                if (!string.IsNullOrWhiteSpace(txtMinAboveEma.Text))
                {
                    if (int.TryParse(txtMinAboveEma.Text, out var minAbove) && minAbove > 0)
                    {
                        filter.MinAboveEmaCount = minAbove;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的大于EMA数量（大于0的整数）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 解析小于EMA数量
                if (!string.IsNullOrWhiteSpace(txtMinBelowEma.Text))
                {
                    if (int.TryParse(txtMinBelowEma.Text, out var minBelow) && minBelow > 0)
                    {
                        filter.MinBelowEmaCount = minBelow;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的小于EMA数量（大于0的整数）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 解析最小成交额
                if (!string.IsNullOrWhiteSpace(txtMinQuoteVolume.Text))
                {
                    if (decimal.TryParse(txtMinQuoteVolume.Text, out var minQuoteVolume) && minQuoteVolume > 0)
                    {
                        filter.MinQuoteVolume = minQuoteVolume;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的最小成交额（大于0的数值）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 解析最小量比
                if (!string.IsNullOrWhiteSpace(txtMinVolumeRatio.Text))
                {
                    if (decimal.TryParse(txtMinVolumeRatio.Text, out var minVolumeRatio) && minVolumeRatio > 0)
                    {
                        filter.MinVolumeRatio = minVolumeRatio;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的最小量比（大于0的数值，单位：%）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 解析最大流通率
                if (!string.IsNullOrWhiteSpace(txtMaxCirculationRate.Text))
                {
                    if (decimal.TryParse(txtMaxCirculationRate.Text, out var maxCirculationRate) && maxCirculationRate > 0 && maxCirculationRate <= 100)
                    {
                        filter.MaxCirculationRate = maxCirculationRate;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的最大流通率（0-100之间的数值，单位：%）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 解析最大流通市值
                if (!string.IsNullOrWhiteSpace(txtMaxCirculatingMarketCap.Text))
                {
                    if (decimal.TryParse(txtMaxCirculatingMarketCap.Text, out var maxCirculatingMarketCap) && maxCirculatingMarketCap > 0)
                    {
                        filter.MaxCirculatingMarketCap = maxCirculatingMarketCap;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的最大流通市值（大于0的数值，单位：USDT）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 解析最大发行市值
                if (!string.IsNullOrWhiteSpace(txtMaxTotalMarketCap.Text))
                {
                    if (decimal.TryParse(txtMaxTotalMarketCap.Text, out var maxTotalMarketCap) && maxTotalMarketCap > 0)
                    {
                        filter.MaxTotalMarketCap = maxTotalMarketCap;
                        hasFilter = true;
                    }
                    else
                    {
                        MessageBox.Show("请输入有效的最大发行市值（大于0的数值，单位：USDT）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 如果没有任何筛选条件，显示提示
                if (!hasFilter)
                {
                    MessageBox.Show("请至少输入一个筛选条件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                txtStatus.Text = "正在筛选...";
                
                // 应用筛选并补充数据
                await RefreshMonitorResultsAsync(filter);
                
                txtStatus.Text = "筛选完成";
                txtProgress.Text = $"筛选结果: {_currentResults.Count} 个合约";
                
                Console.WriteLine($"✅ 筛选完成，共 {_currentResults.Count} 个合约符合条件");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "筛选失败");
                MessageBox.Show($"筛选失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "筛选失败";
            }
        }

        /// <summary>
        /// 清除筛选按钮点击事件
        /// </summary>
        private async void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清空所有筛选输入框
                txtMinAboveEma.Text = string.Empty;
                txtMinBelowEma.Text = string.Empty;
                txtMinQuoteVolume.Text = string.Empty;
                txtMinVolumeRatio.Text = string.Empty;
                txtMaxCirculationRate.Text = string.Empty;
                txtMaxCirculatingMarketCap.Text = string.Empty;
                txtMaxTotalMarketCap.Text = string.Empty;
                
                txtStatus.Text = "正在刷新数据...";
                
                // 刷新监控结果（不应用筛选）
                await RefreshMonitorResultsAsync();
                
                txtStatus.Text = "筛选已清除";
                txtProgress.Text = $"显示全部: {_currentResults.Count} 个合约";
                
                Console.WriteLine($"✅ 筛选已清除，显示全部 {_currentResults.Count} 个合约");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清除筛选失败");
                MessageBox.Show($"清除筛选失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "操作失败";
            }
        }

        /// <summary>
        /// 刷新监控结果
        /// </summary>
        private async Task RefreshMonitorResultsAsync(HourlyEmaFilter? filter = null)
        {
            // 先获取基础结果（只应用EMA相关筛选）
            var baseFilter = new HourlyEmaFilter
            {
                MinAboveEmaCount = filter?.MinAboveEmaCount,
                MinBelowEmaCount = filter?.MinBelowEmaCount
            };
            
            var results = await _hourlyEmaService.GetMonitorResultsAsync(baseFilter.MinAboveEmaCount.HasValue || baseFilter.MinBelowEmaCount.HasValue ? baseFilter : null);
            
            // 补充额外的数据（24h成交额、流通量、发行总量、量比等）
            await EnrichResultsWithAdditionalDataAsync(results);
            
            // 应用客户端筛选（成交额、量比、流通率）
            if (filter != null)
            {
                results = ApplyClientSideFilter(results, filter);
            }
            
            _currentResults = results;
            dgResults.ItemsSource = null;
            dgResults.ItemsSource = _currentResults;
            
            // 统计均线以上和以下的合约数（AboveEmaCount > 0 表示在均线以上，BelowEmaCount > 0 表示在均线以下）
            var aboveEmaCount = _currentResults.Count(r => r.AboveEmaCount > 0);
            var belowEmaCount = _currentResults.Count(r => r.BelowEmaCount > 0);
            
            // 更新统计显示
            txtAboveEmaCount.Text = aboveEmaCount.ToString();
            txtBelowEmaCount.Text = belowEmaCount.ToString();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📊 统计结果：均线以上 {aboveEmaCount} 个，均线以下 {belowEmaCount} 个");
            
            if (filter != null && (filter.MinQuoteVolume.HasValue || filter.MinVolumeRatio.HasValue || filter.MaxCirculationRate.HasValue ||
                filter.MaxCirculatingMarketCap.HasValue || filter.MaxTotalMarketCap.HasValue ||
                filter.MinAboveEmaCount.HasValue || filter.MinBelowEmaCount.HasValue))
            {
                txtResultTitle.Text = $"监控结果 (筛选后: {_currentResults.Count} 个合约)";
            }
            else
            {
                txtResultTitle.Text = $"监控结果 (共 {_currentResults.Count} 个合约)";
            }
        }

        /// <summary>
        /// 应用客户端筛选（针对额外数据）
        /// </summary>
        private List<HourlyEmaMonitorResult> ApplyClientSideFilter(List<HourlyEmaMonitorResult> results, HourlyEmaFilter filter)
        {
            var filtered = results.AsEnumerable();
            
            // 筛选成交额
            if (filter.MinQuoteVolume.HasValue)
            {
                filtered = filtered.Where(r => r.QuoteVolume24h >= filter.MinQuoteVolume.Value);
            }
            
            // 筛选量比
            if (filter.MinVolumeRatio.HasValue)
            {
                filtered = filtered.Where(r => r.VolumeRatio >= filter.MinVolumeRatio.Value);
            }
            
            // 筛选流通率
            if (filter.MaxCirculationRate.HasValue)
            {
                filtered = filtered.Where(r => r.CirculationRate <= filter.MaxCirculationRate.Value);
            }
            
            // 筛选流通市值
            if (filter.MaxCirculatingMarketCap.HasValue)
            {
                filtered = filtered.Where(r => r.CirculatingMarketCap <= filter.MaxCirculatingMarketCap.Value);
            }
            
            // 筛选发行市值
            if (filter.MaxTotalMarketCap.HasValue)
            {
                filtered = filtered.Where(r => r.TotalMarketCap <= filter.MaxTotalMarketCap.Value);
            }
            
            return filtered.ToList();
        }

        /// <summary>
        /// 补充结果的额外数据
        /// </summary>
        private async Task EnrichResultsWithAdditionalDataAsync(List<HourlyEmaMonitorResult> results)
        {
            try
            {
                // 获取所有合约的ticker数据
                var apiClient = _serviceProvider.GetRequiredService<IBinanceSimulatedApiClient>();
                var tickers = await apiClient.GetAllTicksAsync();
                var tickerDict = new Dictionary<string, PriceStatistics>();
                if (tickers != null)
                {
                    foreach (var ticker in tickers)
                    {
                        tickerDict[ticker.Symbol] = ticker;
                    }
                }

                foreach (var result in results)
                {
                    PriceStatistics? ticker = null;
                    tickerDict.TryGetValue(result.Symbol, out ticker);
                    
                    // 24h成交额（从ticker获取，保持原始数值）
                    if (ticker != null)
                    {
                        result.QuoteVolume24h = ticker.QuoteVolume;
                        // 不进行格式化，保持原始数值供后续计算使用
                        result.QuoteVolumeText = ticker.QuoteVolume.ToString("N0"); // 千分位分隔符
                    }

                    // 流通量、发行总量、流通率、量比（从ContractInfoService获取）
                    if (_contractInfoService != null)
                    {
                        var contractInfo = _contractInfoService.GetContractInfo(result.Symbol);
                        if (contractInfo != null)
                        {
                            result.CirculatingSupply = contractInfo.CirculatingSupply;
                            result.TotalSupply = contractInfo.TotalSupply;
                            
                            // 计算流通率
                            result.CirculationRate = contractInfo.TotalSupply > 0
                                ? (contractInfo.CirculatingSupply / contractInfo.TotalSupply * 100)
                                : 0;
                            
                            // 计算流通市值 = LastPrice × CirculatingSupply
                            result.CirculatingMarketCap = result.LastPrice * contractInfo.CirculatingSupply;
                            
                            // 计算发行市值 = LastPrice × TotalSupply
                            result.TotalMarketCap = result.LastPrice * contractInfo.TotalSupply;
                            
                            // 计算量比 = (24h成交额 / 流通市值) × 100，以百分比表示
                            if (result.CirculatingMarketCap > 0 && ticker != null)
                            {
                                result.VolumeRatio = (ticker.QuoteVolume / result.CirculatingMarketCap) * 100;
                            }
                            else
                            {
                                result.VolumeRatio = 0;
                            }
                            
                            // 合约简介
                            result.Description = !string.IsNullOrEmpty(contractInfo.Description) 
                                ? contractInfo.Description 
                                : (!string.IsNullOrEmpty(contractInfo.Symbol) 
                                    ? contractInfo.Symbol 
                                    : $"{contractInfo.Name} 合约");
                        }
                    }
                }
                
                Console.WriteLine($"✅ 数据补充完成，共处理 {results.Count} 个合约");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "补充结果数据失败");
                Console.WriteLine($"⚠️ 补充结果数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前筛选条件
        /// </summary>
        private HourlyEmaFilter? GetCurrentFilter()
        {
            var filter = new HourlyEmaFilter();
            bool hasFilter = false;

            if (!string.IsNullOrWhiteSpace(txtMinAboveEma.Text) && int.TryParse(txtMinAboveEma.Text, out var minAbove) && minAbove > 0)
            {
                filter.MinAboveEmaCount = minAbove;
                hasFilter = true;
            }

            if (!string.IsNullOrWhiteSpace(txtMinBelowEma.Text) && int.TryParse(txtMinBelowEma.Text, out var minBelow) && minBelow > 0)
            {
                filter.MinBelowEmaCount = minBelow;
                hasFilter = true;
            }

            return hasFilter ? filter : null;
        }

        /// <summary>
        /// 打开浮动监控窗口按钮点击事件
        /// </summary>
        private void BtnOpenFloatingMonitor_Click(object sender, RoutedEventArgs e)
        {
            ShowFloatingMonitor();
        }

        /// <summary>
        /// 清除缓存按钮点击事件
        /// </summary>
        private void BtnClearCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("确定要清除所有缓存数据吗？", "确认", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _hourlyEmaService.ClearCache();
                    _currentResults.Clear();
                    dgResults.ItemsSource = null;
                    dgResults.ItemsSource = _currentResults;
                    
                    txtResultTitle.Text = "监控结果 (共 0 个合约)";
                    txtStatus.Text = "缓存已清除";
                    txtProgress.Text = "";
                    
                    btnUpdateKlines.IsEnabled = false;
                    btnCalculate.IsEnabled = false;
                    
                    MessageBox.Show("缓存已清除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清除缓存失败");
                MessageBox.Show($"清除缓存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 数据表格双击事件
        /// </summary>
        private async void DgResults_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgResults.SelectedItem is HourlyEmaMonitorResult selectedResult)
            {
                try
                {
                    // 检查是否按住Ctrl键
                    if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        // Ctrl+双击：打开图表窗口
                        Console.WriteLine($"📊 Ctrl+双击触发，正在打开 {selectedResult.Symbol} 的图表窗口...");
                        
                        // 显示加载状态
                        txtStatus.Text = $"正在加载 {selectedResult.Symbol} 的K线数据...";
                        
                        try
                        {
                            Console.WriteLine($"🔍 开始获取 {selectedResult.Symbol} 的K线数据...");
                            
                            // 获取K线数据
                            var klineData = await _hourlyEmaService.GetHourlyKlineDataAsync(selectedResult.Symbol);
                            
                            Console.WriteLine($"📦 K线数据获取完成: klineData={klineData != null}, Klines={klineData?.Klines?.Count ?? 0}");
                            
                            if (klineData == null)
                            {
                                Console.WriteLine($"❌ K线数据为null");
                                MessageBox.Show($"无法获取 {selectedResult.Symbol} 的K线数据", "错误", 
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                txtStatus.Text = "就绪";
                                return;
                            }
                            
                            if (klineData.Klines == null || klineData.Klines.Count == 0)
                            {
                                Console.WriteLine($"❌ K线数据为空，Klines={klineData.Klines?.Count ?? 0}");
                                MessageBox.Show($"{selectedResult.Symbol} 的K线数据为空，请先点击\"获取小时K线\"按钮", "提示", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                txtStatus.Text = "就绪";
                                return;
                            }
                            
                            Console.WriteLine($"🎨 开始创建图表窗口...");
                            
                            // 创建并显示窗口（已经在UI线程上，不需要Dispatcher）
                            var chartWindow = new KlineChartWindow(selectedResult.Symbol, klineData)
                            {
                                Owner = this
                            };
                            
                            Console.WriteLine($"✅ 图表窗口创建成功，准备显示...");
                            chartWindow.Show();
                            Console.WriteLine($"✅ 图表窗口已显示: {selectedResult.Symbol}");
                            
                            txtStatus.Text = "就绪";
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ 打开图表窗口失败: {ex.Message}");
                            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                            MessageBox.Show($"打开图表窗口失败:\n{ex.Message}", "错误", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            txtStatus.Text = "就绪";
                        }
                    }
                    else
                    {
                        // 普通双击：复制合约名
                        bool success = false;
                        int attempts = 0;
                        const int maxAttempts = 3;
                        
                        while (!success && attempts < maxAttempts)
                        {
                            try
                            {
                                Clipboard.SetText(selectedResult.Symbol);
                                success = true;
                                Console.WriteLine($"📋 已复制合约名到剪贴板: {selectedResult.Symbol}");
                            }
                            catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
                            {
                                attempts++;
                                if (attempts < maxAttempts)
                                {
                                    System.Threading.Thread.Sleep(100);
                                    Console.WriteLine($"⚠️ 剪贴板被占用，正在重试... ({attempts}/{maxAttempts})");
                                }
                            }
                        }
                        
                        if (!success)
                        {
                            Console.WriteLine($"❌ 无法访问剪贴板，请手动复制: {selectedResult.Symbol}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 操作失败: {ex.Message}");
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 显示浮动监控窗口
        /// </summary>
        private void ShowFloatingMonitor()
        {
            if (_floatingMonitor == null || !_floatingMonitor.IsLoaded)
            {
                _floatingMonitor = new FloatingMonitorWindow(_serviceProvider);
                _floatingMonitor.Show();
                Console.WriteLine("✅ 浮动监控窗口已打开");
            }
            else
            {
                _floatingMonitor.Activate();
            }
        }

        /// <summary>
        /// 加入多头监控菜单点击
        /// </summary>
        private void MenuItem_AddToLongMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is HourlyEmaMonitorResult selectedResult)
            {
                try
                {
                    if (_floatingMonitor == null || !_floatingMonitor.IsLoaded)
                    {
                        ShowFloatingMonitor();
                    }

                    _floatingMonitor?.AddMonitorItem(
                        selectedResult.Symbol,
                        MonitorType.Long,
                        selectedResult.LastPrice
                    );
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "加入多头监控失败");
                    MessageBox.Show($"加入多头监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 加入空头监控菜单点击
        /// </summary>
        private void MenuItem_AddToShortMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is HourlyEmaMonitorResult selectedResult)
            {
                try
                {
                    if (_floatingMonitor == null || !_floatingMonitor.IsLoaded)
                    {
                        ShowFloatingMonitor();
                    }

                    _floatingMonitor?.AddMonitorItem(
                        selectedResult.Symbol,
                        MonitorType.Short,
                        selectedResult.LastPrice
                    );
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "加入空头监控失败");
                    MessageBox.Show($"加入空头监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 保存配置
            SaveConfig();
            
            // 不关闭浮动窗口，让它独立存在
            // 如果需要关闭，用户可以手动关闭
            
            base.OnClosing(e);
        }
        
        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    Console.WriteLine("📋 配置文件不存在，使用默认参数");
                    return;
                }

                var json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<HourlyEmaConfig>(json);

                if (config != null)
                {
                    // 加载参数
                    if (config.Parameters != null)
                    {
                        txtEmaPeriod.Text = config.Parameters.EmaPeriod.ToString();
                        txtKlineCount.Text = config.Parameters.KlineCount.ToString();
                    }

                    // 加载筛选条件
                    if (config.Filter != null)
                    {
                        txtMinAboveEma.Text = config.Filter.MinAboveEmaCount?.ToString() ?? "";
                        txtMinBelowEma.Text = config.Filter.MinBelowEmaCount?.ToString() ?? "";
                        txtMinQuoteVolume.Text = config.Filter.MinQuoteVolume?.ToString() ?? "";
                        txtMinVolumeRatio.Text = config.Filter.MinVolumeRatio?.ToString() ?? "";
                        txtMaxCirculationRate.Text = config.Filter.MaxCirculationRate?.ToString() ?? "";
                        txtMaxCirculatingMarketCap.Text = config.Filter.MaxCirculatingMarketCap?.ToString() ?? "";
                        txtMaxTotalMarketCap.Text = config.Filter.MaxTotalMarketCap?.ToString() ?? "";
                    }

                    Console.WriteLine($"✅ 成功加载配置: N={config.Parameters?.EmaPeriod}, X={config.Parameters?.KlineCount}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载配置失败");
                Console.WriteLine($"⚠️ 加载配置失败: {ex.Message}");
                // 加载失败不影响使用，继续使用默认值
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                var config = new HourlyEmaConfig
                {
                    Parameters = new HourlyEmaParameters
                    {
                        EmaPeriod = int.TryParse(txtEmaPeriod.Text, out var emaPeriod) ? emaPeriod : 26,
                        KlineCount = int.TryParse(txtKlineCount.Text, out var klineCount) ? klineCount : 100
                    },
                    Filter = new HourlyEmaFilter
                    {
                        MinAboveEmaCount = int.TryParse(txtMinAboveEma.Text, out var minAbove) ? minAbove : null,
                        MinBelowEmaCount = int.TryParse(txtMinBelowEma.Text, out var minBelow) ? minBelow : null,
                        MinQuoteVolume = decimal.TryParse(txtMinQuoteVolume.Text, out var minVolume) ? minVolume : null,
                        MinVolumeRatio = decimal.TryParse(txtMinVolumeRatio.Text, out var minRatio) ? minRatio : null,
                        MaxCirculationRate = decimal.TryParse(txtMaxCirculationRate.Text, out var maxCirculation) ? maxCirculation : null,
                        MaxCirculatingMarketCap = decimal.TryParse(txtMaxCirculatingMarketCap.Text, out var maxCirculatingCap) ? maxCirculatingCap : null,
                        MaxTotalMarketCap = decimal.TryParse(txtMaxTotalMarketCap.Text, out var maxTotalCap) ? maxTotalCap : null
                    },
                    LastSaved = DateTime.Now
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigFilePath, json);

                Console.WriteLine($"💾 已保存配置: N={config.Parameters.EmaPeriod}, X={config.Parameters.KlineCount}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存配置失败");
                Console.WriteLine($"⚠️ 保存配置失败: {ex.Message}");
                // 保存失败不影响使用
            }
        }
    }
}

