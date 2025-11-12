using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using BinanceApps.Core.Interfaces;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 量比异动选股窗口
    /// </summary>
    public partial class VolumeRatioWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IVolumeRatioService _volumeRatioService;
        private readonly VolumeRatioSettingsService _settingsService;
        private readonly ILogger<VolumeRatioWindow>? _logger;
        private List<VolumeRatioResult> _currentResults = new List<VolumeRatioResult>();
        
        // 监控相关字段
        private bool _isMonitoring = false;
        private System.Windows.Threading.DispatcherTimer? _monitorTimer;
        private List<VolumeRatioResult> _monitorResults = new List<VolumeRatioResult>();

        public VolumeRatioWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _volumeRatioService = _serviceProvider.GetRequiredService<IVolumeRatioService>();
            _settingsService = new VolumeRatioSettingsService();
            _logger = _serviceProvider.GetService<ILogger<VolumeRatioWindow>>();
            
            InitializeWindow();
        }

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private async void InitializeWindow()
        {
            try
            {
                // 加载保存的参数
                await LoadSettingsAsync();

                // 初始化数据网格
                dgResults.ItemsSource = _currentResults;
                
                // 设置状态
                txtStatus.Text = "就绪";
                txtLastUpdate.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                Console.WriteLine("✅ 量比异动选股窗口初始化完成");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "量比异动选股窗口初始化失败");
                MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检索按钮点击事件
        /// </summary>
        private async void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 禁用按钮，显示加载状态
                btnSearch.IsEnabled = false;
                btnSearch.Content = "检索中...";
                txtStatus.Text = "正在检索...";
                
                // 获取筛选条件
                var filter = GetFilterFromUI();
                
                // 保存当前设置
                await SaveSettingsAsync();
                
                // 执行检索
                var results = await _volumeRatioService.SearchVolumeRatioAsync(filter);
                
                // 更新结果
                _currentResults = results;
                dgResults.ItemsSource = _currentResults;
                
                // 更新统计信息
                // 更新结果标题
                txtResultTitle.Text = $"检索结果 (共 {results.Count} 个)";
                txtLastUpdate.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                // 恢复按钮状态
                btnSearch.IsEnabled = true;
                btnSearch.Content = "🔍 检索";
                txtStatus.Text = "检索完成";
                
                Console.WriteLine($"✅ 量比异动选股完成，找到 {results.Count} 个符合条件的合约");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "量比异动选股检索失败");
                MessageBox.Show($"检索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复按钮状态
                btnSearch.IsEnabled = true;
                btnSearch.Content = "🔍 检索";
                txtStatus.Text = "检索失败";
            }
        }

        /// <summary>
        /// 清空条件按钮点击事件
        /// </summary>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 重置所有筛选条件
                txtMinMarketCap.Text = "0";
                txtMaxMarketCap.Text = "1000000000";
                txtMinVolumeRatio.Text = "0.1";
                txtMaxVolumeRatio.Text = "10";
                txtMin24HVolume.Text = "1000000";
                txtMax24HVolume.Text = "1000000000";
                txtMaDistance.Text = "3";
                rbLong.IsChecked = true;
                
                // 清空结果
                _currentResults.Clear();
                dgResults.ItemsSource = _currentResults;
                txtResultTitle.Text = "检索结果 (共 0 个)";
                txtStatus.Text = "条件已清空";
                
                Console.WriteLine("✅ 筛选条件已清空");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清空筛选条件失败");
                MessageBox.Show($"清空条件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出结果按钮点击事件
        /// </summary>
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentResults == null || !_currentResults.Any())
                {
                    MessageBox.Show("没有可导出的数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 创建保存文件对话框
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出量比异动选股结果",
                    Filter = "JSON文件 (*.json)|*.json|CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"量比异动选股结果_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var fileName = saveFileDialog.FileName;
                    var extension = Path.GetExtension(fileName).ToLower();

                    if (extension == ".json")
                    {
                        await ExportToJsonAsync(fileName);
                    }
                    else if (extension == ".csv")
                    {
                        await ExportToCsvAsync(fileName);
                    }
                    else
                    {
                        MessageBox.Show("不支持的文件格式", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    MessageBox.Show($"导出成功！\n文件保存位置: {fileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    Console.WriteLine($"✅ 量比异动选股结果已导出到: {fileName}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "导出量比异动选股结果失败");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 加载保存的设置
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                var filter = await _settingsService.LoadFilterAsync();
                if (filter != null)
                {
                    // 应用加载的参数到UI
                    txtMinMarketCap.Text = filter.MinMarketCap?.ToString() ?? "0";
                    txtMaxMarketCap.Text = filter.MaxMarketCap?.ToString() ?? "1000000000";
                    txtMinVolumeRatio.Text = filter.MinVolumeRatio?.ToString() ?? "0.1";
                    txtMaxVolumeRatio.Text = filter.MaxVolumeRatio?.ToString() ?? "10";
                    txtMin24HVolume.Text = filter.Min24HVolume?.ToString() ?? "1000000";
                    txtMax24HVolume.Text = filter.Max24HVolume?.ToString() ?? "1000000000";
                    txtMaDistance.Text = filter.MaDistancePercent.ToString();
                    rbLong.IsChecked = filter.IsLong;
                    rbShort.IsChecked = !filter.IsLong;
                    
                    Console.WriteLine("✅ 已加载保存的筛选参数");
                }
                else
                {
                    // 使用默认值
                    SetDefaultValues();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载设置失败");
                Console.WriteLine($"❌ 加载设置失败: {ex.Message}");
                SetDefaultValues();
            }
        }

        /// <summary>
        /// 保存当前设置
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                var filter = GetFilterFromUI();
                await _settingsService.SaveFilterAsync(filter);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存设置失败");
                Console.WriteLine($"❌ 保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置默认值
        /// </summary>
        private void SetDefaultValues()
        {
            txtMinMarketCap.Text = "0";
            txtMaxMarketCap.Text = "100000"; // 100000万 = 10亿
            txtMinVolumeRatio.Text = "0.1";
            txtMaxVolumeRatio.Text = "10";
            txtMin24HVolume.Text = "100"; // 100万
            txtMax24HVolume.Text = "100000"; // 100000万 = 10亿
            txtMaDistance.Text = "3";
            txtMaPeriod.Text = "26";
            txtSameSideCount.Text = "10";
            rbLong.IsChecked = true;
        }

        /// <summary>
        /// 从UI获取筛选条件
        /// </summary>
        private VolumeRatioFilter GetFilterFromUI()
        {
            return new VolumeRatioFilter
            {
                MinMarketCap = decimal.TryParse(txtMinMarketCap.Text, out var minMarketCap) ? minMarketCap : null,
                MaxMarketCap = decimal.TryParse(txtMaxMarketCap.Text, out var maxMarketCap) ? maxMarketCap : null,
                MinVolumeRatio = decimal.TryParse(txtMinVolumeRatio.Text, out var minVolumeRatio) ? minVolumeRatio : null,
                MaxVolumeRatio = decimal.TryParse(txtMaxVolumeRatio.Text, out var maxVolumeRatio) ? maxVolumeRatio : null,
                Min24HVolume = decimal.TryParse(txtMin24HVolume.Text, out var min24HVolume) ? min24HVolume : null,
                Max24HVolume = decimal.TryParse(txtMax24HVolume.Text, out var max24HVolume) ? max24HVolume : null,
                MaDistancePercent = decimal.TryParse(txtMaDistance.Text, out var maDistance) ? maDistance : 3.0m,
                MaPeriod = int.TryParse(txtMaPeriod.Text, out var maPeriod) ? maPeriod : 26,
                SameSideCount = int.TryParse(txtSameSideCount.Text, out var sameSideCount) ? sameSideCount : 10,
                IsLong = rbLong.IsChecked == true
            };
        }

        /// <summary>
        /// 导出为JSON格式
        /// </summary>
        private async Task ExportToJsonAsync(string fileName)
        {
            var exportData = new
            {
                ExportTime = DateTime.Now,
                Filter = GetFilterFromUI(),
                Results = _currentResults,
                TotalCount = _currentResults.Count
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(fileName, json);
        }

        /// <summary>
        /// 导出为CSV格式
        /// </summary>
        private async Task ExportToCsvAsync(string fileName)
        {
            var csvLines = new List<string>
            {
                "合约名,24H涨幅(%),24H成交额,流通市值,总市值,流通比例(%),量比,26H均线距离(%),最新价格,26H均线价格"
            };

            foreach (var result in _currentResults)
            {
                var line = $"{result.Symbol}," +
                          $"{result.PriceChangePercent:F2}," +
                          $"{result.Volume24H:N0}," +
                          $"{result.CirculatingMarketCap:N0}," +
                          $"{result.TotalMarketCap:N0}," +
                          $"{result.CirculatingRatio:P2}," +
                          $"{result.VolumeRatio:F2}," +
                          $"{result.MaDistancePercent:F2}," +
                          $"{result.LastPrice:F8}," +
                          $"{result.Ma26Price:F8}";
                csvLines.Add(line);
            }

            await File.WriteAllTextAsync(fileName, string.Join("\n", csvLines));
        }

        /// <summary>
        /// 监控按钮点击事件
        /// </summary>
        private void BtnMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoring)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        /// <summary>
        /// 开始监控
        /// </summary>
        private void StartMonitoring()
        {
            if (_currentResults.Count == 0)
            {
                MessageBox.Show("请先进行检索，获取结果后再开始监控", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isMonitoring = true;
            btnMonitor.Content = "停止监控";
            btnMonitor.Background = new SolidColorBrush(Colors.Red);
            btnSearch.IsEnabled = false;
            btnSearch.Background = new SolidColorBrush(Colors.Gray);

            // 创建定时器，每5分钟执行一次
            _monitorTimer = new System.Windows.Threading.DispatcherTimer();
            _monitorTimer.Interval = TimeSpan.FromMinutes(5);
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();

            txtStatus.Text = "监控中...";
            Console.WriteLine("✅ 开始监控，每5分钟更新一次数据");
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        private void StopMonitoring()
        {
            _isMonitoring = false;
            btnMonitor.Content = "开始监控";
            btnMonitor.Background = new SolidColorBrush(Colors.Green);
            btnSearch.IsEnabled = true;
            btnSearch.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));

            if (_monitorTimer != null)
            {
                _monitorTimer.Stop();
                _monitorTimer = null;
            }

            txtStatus.Text = "监控已停止";
            Console.WriteLine("⏹️ 停止监控");
        }

        /// <summary>
        /// 监控定时器事件
        /// </summary>
        private async void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("🔄 开始监控数据更新...");
                txtStatus.Text = "正在更新监控数据...";

                // 获取最新的ticker数据
                var apiClient = _serviceProvider.GetRequiredService<IBinanceSimulatedApiClient>();
                var allTicks = await apiClient.GetAllTicksAsync();
                
                if (allTicks == null || !allTicks.Any())
                {
                    Console.WriteLine("❌ 无法获取最新ticker数据");
                    return;
                }

                // 更新当前结果列表中的价格和距离
                var updatedResults = new List<VolumeRatioResult>();
                var monitorCandidates = new List<VolumeRatioResult>();

                foreach (var currentResult in _currentResults)
                {
                    // 查找对应的最新ticker数据
                    var latestTick = allTicks.FirstOrDefault(t => t.Symbol == currentResult.Symbol);
                    if (latestTick != null)
                    {
                        // 获取最新的均线距离和同侧K线数
                        var filter = GetFilterFromUI();
                        var (maDistance, sameSideCloseCount, sameSideExtremeCount, maPrice) = await GetMaDistanceAndSameSideCountAsync(currentResult.Symbol, latestTick.LastPrice, filter.MaPeriod);
                        
                        if (maDistance.HasValue)
                        {
                            // 更新结果（金额转换为万为单位）
                            var updatedResult = new VolumeRatioResult
                            {
                                Symbol = currentResult.Symbol,
                                PriceChangePercent = latestTick.PriceChangePercent,
                                Volume24H = latestTick.QuoteVolume / 10000, // 转换为万
                                CirculatingMarketCap = currentResult.CirculatingMarketCap, // 已经是万为单位
                                TotalMarketCap = currentResult.TotalMarketCap, // 已经是万为单位
                                CirculatingRatio = currentResult.CirculatingRatio,
                                VolumeRatio = currentResult.VolumeRatio,
                                MaDistancePercent = maDistance.Value,
                                LastPrice = latestTick.LastPrice,
                                Ma26Price = maPrice,
                                CirculatingSupply = currentResult.CirculatingSupply,
                                TotalSupply = currentResult.TotalSupply,
                                SameSideCloseCount = sameSideCloseCount,
                                SameSideExtremeCount = sameSideExtremeCount,
                                UpdateTime = DateTime.Now
                            };

                            updatedResults.Add(updatedResult);

                            // 检查是否符合距离监控条件
                            var absDistance = Math.Abs(maDistance.Value);
                            Console.WriteLine($"🔍 监控检查: {updatedResult.Symbol} 距离={maDistance.Value:F2}% 绝对值={absDistance:F2}% 阈值={filter.MaDistancePercent}%");
                            
                            if (absDistance <= filter.MaDistancePercent)
                            {
                                Console.WriteLine($"✅ 符合监控条件: {updatedResult.Symbol} 绝对值{absDistance:F2}% <= 阈值{filter.MaDistancePercent}%");
                                monitorCandidates.Add(updatedResult);
                                
                                // 显示弹窗预警并播放系统声音
                                ShowMonitoringAlert(updatedResult, maDistance.Value);
                            }
                            else
                            {
                                Console.WriteLine($"❌ 不符合监控条件: {updatedResult.Symbol} 绝对值{absDistance:F2}% > 阈值{filter.MaDistancePercent}%");
                            }
                        }
                    }
                }

                // 更新UI
                _currentResults = updatedResults;
                dgResults.ItemsSource = _currentResults;

                // 更新监控列表
                _monitorResults = monitorCandidates;
                dgMonitorResults.ItemsSource = _monitorResults;

                txtStatus.Text = $"监控中... (发现 {monitorCandidates.Count} 个符合条件)";
                txtLastUpdate.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                Console.WriteLine($"✅ 监控更新完成，发现 {monitorCandidates.Count} 个符合距离条件的合约");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "监控更新失败");
                Console.WriteLine($"❌ 监控更新失败: {ex.Message}");
                txtStatus.Text = "监控更新失败";
            }
        }

        /// <summary>
        /// 主结果列表双击事件
        /// </summary>
        private void DgResults_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgResults.SelectedItem is VolumeRatioResult selectedResult)
            {
                try
                {
                    // 尝试多次复制到剪贴板，处理剪贴板被占用的情况
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
                            // CLIPBRD_E_CANT_OPEN - 剪贴板被占用
                            attempts++;
                            if (attempts < maxAttempts)
                            {
                                System.Threading.Thread.Sleep(100); // 等待100毫秒后重试
                                Console.WriteLine($"⚠️ 剪贴板被占用，正在重试... ({attempts}/{maxAttempts})");
                            }
                        }
                    }
                    
                    if (!success)
                    {
                        Console.WriteLine($"❌ 无法访问剪贴板，请手动复制: {selectedResult.Symbol}");
                        // 可以考虑显示一个消息框提示用户手动复制
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 复制到剪贴板失败: {ex.Message}");
                    Console.WriteLine($"📋 请手动复制合约名: {selectedResult.Symbol}");
                }
            }
        }

        /// <summary>
        /// 监控结果列表双击事件
        /// </summary>
        private void DgMonitorResults_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgMonitorResults.SelectedItem is VolumeRatioResult selectedResult)
            {
                try
                {
                    // 尝试多次复制到剪贴板，处理剪贴板被占用的情况
                    bool success = false;
                    int attempts = 0;
                    const int maxAttempts = 3;
                    
                    while (!success && attempts < maxAttempts)
                    {
                        try
                        {
                            Clipboard.SetText(selectedResult.Symbol);
                            success = true;
                            Console.WriteLine($"📋 已复制监控合约名到剪贴板: {selectedResult.Symbol}");
                        }
                        catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
                        {
                            // CLIPBRD_E_CANT_OPEN - 剪贴板被占用
                            attempts++;
                            if (attempts < maxAttempts)
                            {
                                System.Threading.Thread.Sleep(100); // 等待100毫秒后重试
                                Console.WriteLine($"⚠️ 剪贴板被占用，正在重试... ({attempts}/{maxAttempts})");
                            }
                        }
                    }
                    
                    if (!success)
                    {
                        Console.WriteLine($"❌ 无法访问剪贴板，请手动复制: {selectedResult.Symbol}");
                        // 可以考虑显示一个消息框提示用户手动复制
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 复制到剪贴板失败: {ex.Message}");
                    Console.WriteLine($"📋 请手动复制监控合约名: {selectedResult.Symbol}");
                }
            }
        }

        /// <summary>
        /// 计算均线距离和同侧K线数
        /// </summary>
        private async Task<(decimal? MaDistance, int SameSideCloseCount, int SameSideExtremeCount, decimal MaPrice)> GetMaDistanceAndSameSideCountAsync(string symbol, decimal currentPrice, int maPeriod)
        {
            try
            {
                var klineService = _serviceProvider.GetRequiredService<KlineDataStorageService>();
                var (klines, success, errorMessage) = await klineService.LoadKlineDataAsync(symbol);
                if (!success || klines == null || klines.Count < maPeriod)
                {
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
                Console.WriteLine($"📊 {symbol} 监控计算过程：");
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
                _logger?.LogError(ex, $"计算合约 {symbol} 的{maPeriod}小时均线距离失败");
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
        /// 显示监控预警弹窗
        /// </summary>
        private void ShowMonitoringAlert(VolumeRatioResult result, decimal maDistance)
        {
            try
            {
                // 播放系统声音
                System.Media.SystemSounds.Exclamation.Play();
                
                // 创建预警窗口
                var alertWindow = new Window
                {
                    Title = "🚨 距离监控预警",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Topmost = true,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(255, 248, 220)), // 淡黄色背景
                    BorderBrush = new SolidColorBrush(Color.FromRgb(255, 140, 0)), // 橙色边框
                    BorderThickness = new Thickness(3)
                };

                // 创建内容面板
                var mainPanel = new StackPanel
                {
                    Margin = new Thickness(20),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 预警标题
                var titleText = new TextBlock
                {
                    Text = "🚨 距离监控预警",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 0)), // 橙红色
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                };

                // 合约信息
                var symbolText = new TextBlock
                {
                    Text = $"合约: {result.Symbol}",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                // 距离信息
                var distanceText = new TextBlock
                {
                    Text = $"均线距离: {maDistance:F2}%",
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                // 价格信息
                var priceText = new TextBlock
                {
                    Text = $"当前价格: {result.LastPrice:F8}",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                // 时间信息
                var timeText = new TextBlock
                {
                    Text = $"预警时间: {DateTime.Now:HH:mm:ss}",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray)
                };

                // 添加到面板
                mainPanel.Children.Add(titleText);
                mainPanel.Children.Add(symbolText);
                mainPanel.Children.Add(distanceText);
                mainPanel.Children.Add(priceText);
                mainPanel.Children.Add(timeText);

                alertWindow.Content = mainPanel;

                // 显示窗口
                alertWindow.Show();

                // 60秒后自动关闭
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(60)
                };
                timer.Tick += (sender, e) =>
                {
                    timer.Stop();
                    alertWindow.Close();
                };
                timer.Start();

                Console.WriteLine($"🚨 监控预警: {result.Symbol} 距离 {maDistance:F2}% 触发预警条件");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 显示监控预警失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 价格变化颜色转换器
    /// </summary>
    public class PriceChangeColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is decimal priceChange)
            {
                return priceChange >= 0 ? Brushes.Red : Brushes.Green;
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 量比颜色转换器
    /// </summary>
    public class VolumeRatioColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is decimal volumeRatio)
            {
                if (volumeRatio >= 2.0m) return Brushes.Red;      // 高量比
                if (volumeRatio >= 1.5m) return Brushes.Orange;   // 中量比
                return Brushes.Green;                              // 低量比
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 均线距离颜色转换器
    /// </summary>
    public class MaDistanceColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is decimal maDistance)
            {
                if (maDistance >= 0) return Brushes.Red;      // 均线上方
                return Brushes.Green;                          // 均线下方
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
