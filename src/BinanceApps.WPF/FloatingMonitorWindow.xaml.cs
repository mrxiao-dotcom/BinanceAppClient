using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BinanceApps.Core.Models;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Services;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 浮动监控窗口
    /// </summary>
    public partial class FloatingMonitorWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly IHourlyEmaService _hourlyEmaService;
        private readonly ILogger<FloatingMonitorWindow>? _logger;

        private FloatingMonitorConfig _config = new FloatingMonitorConfig();
        private List<MonitorAlert> _alerts = new List<MonitorAlert>();
        private DispatcherTimer? _monitorTimer;
        private DispatcherTimer? _cleanupTimer;
        private bool _isMonitoring = false;
        private readonly string _configFilePath = "floating_monitor_config.json";
        
        // 企业微信Webhook配置
        private readonly string _wechatWebhookUrl = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=e12bdda2-487f-4f78-972f-716d2ec45dd1";
        private WeChatWebhookService? _wechatService;

        public FloatingMonitorWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            
            _serviceProvider = serviceProvider;
            _apiClient = _serviceProvider.GetRequiredService<IBinanceSimulatedApiClient>();
            _hourlyEmaService = _serviceProvider.GetRequiredService<IHourlyEmaService>();
            _logger = _serviceProvider.GetService<ILogger<FloatingMonitorWindow>>();

            // 设置窗口初始位置（右上角）
            this.Left = SystemParameters.WorkArea.Width - this.Width - 20;
            this.Top = 20;

            InitializeWindow();
        }

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            // 加载配置
            LoadConfig();

            // 设置UI
            txtLongAlertRange.Text = _config.LongAlertRange.ToString();
            txtShortAlertRange.Text = _config.ShortAlertRange.ToString();

            // 绑定数据
            dgLongMonitors.ItemsSource = _config.LongMonitors;
            dgShortMonitors.ItemsSource = _config.ShortMonitors;
            dgAlerts.ItemsSource = _alerts;

            // 初始化企业微信推送服务
            _wechatService = new WeChatWebhookService(_wechatWebhookUrl, _logger as ILogger<WeChatWebhookService>);

            // 启动清理定时器（每30分钟检查一次）
            StartCleanupTimer();

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 浮动监控窗口初始化完成");
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var config = JsonSerializer.Deserialize<FloatingMonitorConfig>(json);
                    if (config != null)
                    {
                        _config = config;
                        
                        // 重置所有监控项的预警状态（启动程序时重新开始）
                        foreach (var monitor in _config.LongMonitors)
                        {
                            monitor.IsAlerted = false;
                        }
                        foreach (var monitor in _config.ShortMonitors)
                        {
                            monitor.IsAlerted = false;
                        }
                        
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 加载监控配置: 多头{_config.LongMonitors.Count}个，空头{_config.ShortMonitors.Count}个");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 已重置所有监控项的预警状态");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载监控配置失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 加载监控配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                // 更新预警参数
                if (decimal.TryParse(txtLongAlertRange.Text, out var longRange))
                {
                    _config.LongAlertRange = longRange;
                }
                if (decimal.TryParse(txtShortAlertRange.Text, out var shortRange))
                {
                    _config.ShortAlertRange = shortRange;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configFilePath, json);
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 监控配置已保存");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存监控配置失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 保存监控配置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动监控
        /// </summary>
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isMonitoring)
                {
                    MessageBox.Show("监控已经在运行中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 验证参数
                if (!decimal.TryParse(txtLongAlertRange.Text, out var longRange) || longRange <= 0 || longRange > 100)
                {
                    MessageBox.Show("请输入有效的多头预警范围（0-100）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (!decimal.TryParse(txtShortAlertRange.Text, out var shortRange) || shortRange <= 0 || shortRange > 100)
                {
                    MessageBox.Show("请输入有效的空头预警范围（0-100）", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _config.LongAlertRange = longRange;
                _config.ShortAlertRange = shortRange;

                // 启动定时器
                _monitorTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(_config.MonitorIntervalMinutes)
                };
                _monitorTimer.Tick += MonitorTimer_Tick;
                _monitorTimer.Start();

                _isMonitoring = true;
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                txtStatus.Text = "监控中...";
                txtStatus.Foreground = new SolidColorBrush(Colors.Green);

                // 输出预警范围说明
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 预警范围配置:");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]    多头预警: 当距离EMA在 0% 到 +{_config.LongAlertRange}% 之间时触发（价格略高于EMA）");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]    空头预警: 当距离EMA在 -{_config.ShortAlertRange}% 到 0% 之间时触发（价格略低于EMA）");
                
                // 立即执行一次监控
                _ = ExecuteMonitoringAsync();

                SaveConfig();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 监控已启动");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启动监控失败");
                MessageBox.Show($"启动监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isMonitoring)
                {
                    return;
                }

                _monitorTimer?.Stop();
                _monitorTimer = null;
                _isMonitoring = false;

                btnStart.IsEnabled = true;
                btnStop.IsEnabled = false;
                txtStatus.Text = "已停止";
                txtStatus.Foreground = new SolidColorBrush(Colors.Gray);

                SaveConfig();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 监控已停止");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "停止监控失败");
            }
        }

        /// <summary>
        /// 定时器触发事件
        /// </summary>
        private async void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            await ExecuteMonitoringAsync();
        }

        /// <summary>
        /// 执行监控
        /// </summary>
        private async Task ExecuteMonitoringAsync()
        {
            try
            {
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] ⏰ ========== 开始执行监控 ==========");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📊 监控项目: 多头 {_config.LongMonitors.Count} 个, 空头 {_config.ShortMonitors.Count} 个");

                var allMonitors = _config.LongMonitors.Concat(_config.ShortMonitors).ToList();
                if (allMonitors.Count == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 没有监控项目");
                    return;
                }

                // 步骤1：检查并更新K线数据（确保是最新的）
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 检查K线数据是否需要更新...");
                var hoursSinceLastKline = _hourlyEmaService.GetHoursSinceLastKline();
                if (hoursSinceLastKline >= 1.0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ K线数据距离现在 {hoursSinceLastKline:F1} 小时，开始增量更新...");
                    await _hourlyEmaService.UpdateHourlyKlinesAsync();
                    
                    // 更新后重新计算EMA
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔢 重新计算EMA...");
                    await _hourlyEmaService.CalculateAboveBelowEmaCountsAsync();
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ K线数据是最新的（{hoursSinceLastKline:F1} 小时前）");
                }

                // 步骤2：获取所有合约的最新价格
                var tickers = await _apiClient.GetAllTicksAsync();
                var tickerDict = tickers?.ToDictionary(t => t.Symbol) ?? new Dictionary<string, PriceStatistics>();

                foreach (var monitor in allMonitors)
                {
                    try
                    {
                        // 获取最新价格
                        if (!tickerDict.TryGetValue(monitor.Symbol, out var ticker))
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 无法获取 {monitor.Symbol} 的价格数据");
                            continue;
                        }

                        monitor.LastPrice = ticker.LastPrice;

                        // 步骤3：更新最新价格并重新计算EMA
                        var updateSuccess = await _hourlyEmaService.UpdateSymbolLatestPriceAndEmaAsync(monitor.Symbol, ticker.LastPrice);
                        if (!updateSuccess)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 无法更新 {monitor.Symbol} 的EMA数据");
                            continue;
                        }

                        // 获取更新后的K线数据和EMA
                        var klineData = await _hourlyEmaService.GetHourlyKlineDataAsync(monitor.Symbol);
                        if (klineData == null || klineData.EmaValues.Count == 0)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 无法获取 {monitor.Symbol} 的EMA数据");
                            continue;
                        }

                        // 获取最新EMA
                        var latestEma = klineData.EmaValues.Values.Last();
                        monitor.CurrentEma = latestEma;

                        // 步骤4：计算距离百分比
                        monitor.DistancePercent = latestEma != 0 
                            ? ((monitor.LastPrice - latestEma) / latestEma * 100) 
                            : 0;

                        monitor.LastUpdateTime = DateTime.Now;

                        // 步骤5：检查预警条件
                        CheckAlert(monitor);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"处理监控项 {monitor.Symbol} 失败");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 处理 {monitor.Symbol} 失败: {ex.Message}");
                    }
                }

                // 刷新显示
                RefreshDisplay();

                txtStatus.Text = $"监控中 (最后更新: {DateTime.Now:HH:mm:ss})";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 监控完成，当前预警总数: {_alerts.Count}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ========== 监控执行完毕 ==========\n");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行监控失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 执行监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查预警条件
        /// </summary>
        private void CheckAlert(MonitorItem monitor)
        {
            bool shouldAlert = false;
            decimal lowerThreshold = 0;
            decimal upperThreshold = 0;

            if (monitor.Type == MonitorType.Long)
            {
                // 多头预警逻辑：价格在EMA上方，距离在 0% 到 预警范围% 之间时预警
                // 例如：EMA=100, 预警范围=5%, 则当价格在 100 到 105 之间时预警
                lowerThreshold = monitor.CurrentEma;
                upperThreshold = monitor.CurrentEma * (1 + _config.LongAlertRange / 100);
                
                // 距离百分比在 0% 到 +预警范围% 之间
                if (monitor.DistancePercent >= 0 && monitor.DistancePercent <= _config.LongAlertRange)
                {
                    shouldAlert = true;
                }
                
                // 输出调试信息
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 多头检查 {monitor.Symbol}:");
                Console.WriteLine($"    价格={monitor.LastPrice:F8}, EMA={monitor.CurrentEma:F8}");
                Console.WriteLine($"    距离EMA={monitor.DistancePercent:F2}%, 预警范围=0%~{_config.LongAlertRange:F0}%");
                Console.WriteLine($"    预警区间=[{lowerThreshold:F8}, {upperThreshold:F8}]");
                Console.WriteLine($"    符合预警={shouldAlert}, 已预警={monitor.IsAlerted}");
            }
            else // Short
            {
                // 空头预警逻辑：价格在EMA下方，距离在 -预警范围% 到 0% 之间时预警
                // 例如：EMA=100, 预警范围=5%, 则当价格在 95 到 100 之间时预警
                lowerThreshold = monitor.CurrentEma * (1 - _config.ShortAlertRange / 100);
                upperThreshold = monitor.CurrentEma;
                
                // 距离百分比在 -预警范围% 到 0% 之间
                if (monitor.DistancePercent <= 0 && monitor.DistancePercent >= -_config.ShortAlertRange)
                {
                    shouldAlert = true;
                }
                
                // 输出调试信息
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 空头检查 {monitor.Symbol}:");
                Console.WriteLine($"    价格={monitor.LastPrice:F8}, EMA={monitor.CurrentEma:F8}");
                Console.WriteLine($"    距离EMA={monitor.DistancePercent:F2}%, 预警范围=-{_config.ShortAlertRange:F0}%~0%");
                Console.WriteLine($"    预警区间=[{lowerThreshold:F8}, {upperThreshold:F8}]");
                Console.WriteLine($"    符合预警={shouldAlert}, 已预警={monitor.IsAlerted}");
            }

            // 如果符合预警条件且还没有预警过，则触发预警
            if (shouldAlert && !monitor.IsAlerted)
            {
                monitor.IsAlerted = true;

                var alert = new MonitorAlert
                {
                    Symbol = monitor.Symbol,
                    Type = monitor.Type,
                    EntryPrice = monitor.EntryPrice,
                    AlertPrice = monitor.LastPrice,
                    CurrentEma = monitor.CurrentEma,
                    DistancePercent = monitor.DistancePercent,
                    AlertTime = DateTime.Now
                };

                _alerts.Add(alert);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 预警触发: {monitor.Symbol} ({alert.TypeText}) 价格:{alert.AlertPrice:F8} EMA:{alert.CurrentEma:F8} 距离:{alert.DistancePercent:F2}%");
                
                // 发送企业微信通知
                _ = SendWeChatAlertAsync(alert);
            }
            else if (!shouldAlert && monitor.IsAlerted)
            {
                // 如果不再符合预警条件，重置预警状态（这样价格再次进入预警区域时可以再次预警）
                monitor.IsAlerted = false;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 {monitor.Symbol} 预警状态已重置（价格已离开预警区域）");
            }
        }

        /// <summary>
        /// 刷新显示
        /// </summary>
        private void RefreshDisplay()
        {
            dgLongMonitors.Items.Refresh();
            dgShortMonitors.Items.Refresh();
            dgAlerts.Items.Refresh();
        }

        /// <summary>
        /// 添加监控项
        /// </summary>
        public void AddMonitorItem(string symbol, MonitorType type, decimal currentPrice)
        {
            try
            {
                var targetList = type == MonitorType.Long ? _config.LongMonitors : _config.ShortMonitors;

                // 检查是否已存在
                if (targetList.Any(m => m.Symbol == symbol))
                {
                    MessageBox.Show($"{symbol} 已经在{(type == MonitorType.Long ? "多头" : "空头")}监控列表中", 
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var monitor = new MonitorItem
                {
                    Symbol = symbol,
                    Type = type,
                    EntryPrice = currentPrice,
                    EntryTime = DateTime.Now,
                    LastPrice = currentPrice,
                    CurrentEma = 0,
                    DistancePercent = 0,
                    IsAlerted = false
                };

                targetList.Add(monitor);
                RefreshDisplay();
                SaveConfig();

                Console.WriteLine($"✅ 添加监控: {symbol} 到{(type == MonitorType.Long ? "多头" : "空头")}列表，价格:{currentPrice:F8}");
                
                MessageBox.Show($"已添加 {symbol} 到{(type == MonitorType.Long ? "多头" : "空头")}监控列表", 
                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "添加监控项失败");
                MessageBox.Show($"添加监控项失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 添加按钮点击
        /// </summary>
        private void BtnAddMonitor_Click(object sender, RoutedEventArgs e)
        {
            // 弹出输入对话框
            var inputWindow = new AddMonitorDialog(_serviceProvider)
            {
                Owner = this
            };
            if (inputWindow.ShowDialog() == true)
            {
                AddMonitorItem(inputWindow.Symbol, inputWindow.MonitorType, inputWindow.EntryPrice);
            }
        }

        /// <summary>
        /// 删除按钮点击
        /// </summary>
        private void BtnRemoveMonitor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MonitorItem? selectedItem = null;

                if (dgLongMonitors.SelectedItem is MonitorItem longItem)
                {
                    selectedItem = longItem;
                    _config.LongMonitors.Remove(longItem);
                }
                else if (dgShortMonitors.SelectedItem is MonitorItem shortItem)
                {
                    selectedItem = shortItem;
                    _config.ShortMonitors.Remove(shortItem);
                }

                if (selectedItem != null)
                {
                    RefreshDisplay();
                    SaveConfig();
                    Console.WriteLine($"✅ 删除监控: {selectedItem.Symbol}");
                    MessageBox.Show($"已删除 {selectedItem.Symbol} 的监控", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("请先选择要删除的监控项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除监控项失败");
                MessageBox.Show($"删除监控项失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 多头监控列表双击事件 - 复制合约名
        /// </summary>
        private void DgLongMonitors_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgLongMonitors.SelectedItem is MonitorItem selectedMonitor)
            {
                CopySymbolToClipboard(selectedMonitor.Symbol);
            }
        }

        /// <summary>
        /// 空头监控列表双击事件 - 复制合约名
        /// </summary>
        private void DgShortMonitors_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgShortMonitors.SelectedItem is MonitorItem selectedMonitor)
            {
                CopySymbolToClipboard(selectedMonitor.Symbol);
            }
        }

        /// <summary>
        /// 预警列表双击事件 - 复制合约名
        /// </summary>
        private void DgAlerts_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgAlerts.SelectedItem is MonitorAlert selectedAlert)
            {
                CopySymbolToClipboard(selectedAlert.Symbol);
            }
        }

        /// <summary>
        /// 复制合约名到剪贴板（通用方法）
        /// </summary>
        private void CopySymbolToClipboard(string symbol)
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
                        Clipboard.SetText(symbol);
                        success = true;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 已复制合约名到剪贴板: {symbol}");
                    }
                    catch (System.Runtime.InteropServices.COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
                    {
                        // CLIPBRD_E_CANT_OPEN - 剪贴板被占用
                        attempts++;
                        if (attempts < maxAttempts)
                        {
                            System.Threading.Thread.Sleep(100); // 等待100毫秒后重试
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 剪贴板被占用，正在重试... ({attempts}/{maxAttempts})");
                        }
                    }
                }
                
                if (!success)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 无法访问剪贴板，请手动复制: {symbol}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "复制合约名失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 复制失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动清理定时器
        /// </summary>
        private void StartCleanupTimer()
        {
            _cleanupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(30) // 每30分钟检查一次
            };
            _cleanupTimer.Tick += CleanupTimer_Tick;
            _cleanupTimer.Start();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 预警清理定时器已启动（每30分钟检查一次）");
        }

        /// <summary>
        /// 清理定时器触发事件
        /// </summary>
        private void CleanupTimer_Tick(object? sender, EventArgs e)
        {
            CleanupOldAlerts();
        }

        /// <summary>
        /// 清理超过2小时的预警记录
        /// </summary>
        private void CleanupOldAlerts()
        {
            try
            {
                var now = DateTime.Now;
                var twoHoursAgo = now.AddHours(-2);
                
                var oldAlerts = _alerts.Where(a => a.AlertTime < twoHoursAgo).ToList();
                
                if (oldAlerts.Count > 0)
                {
                    foreach (var alert in oldAlerts)
                    {
                        _alerts.Remove(alert);
                    }
                    
                    RefreshDisplay();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑️ 清理了 {oldAlerts.Count} 条超过2小时的预警记录");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理预警记录失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 清理预警记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送企业微信预警通知
        /// </summary>
        private async Task SendWeChatAlertAsync(MonitorAlert alert)
        {
            try
            {
                if (_wechatService == null)
                {
                    return;
                }

                var monitorType = alert.Type == MonitorType.Long ? "多头" : "空头";
                await _wechatService.SendAlertAsync(
                    alert.Symbol,
                    monitorType,
                    alert.AlertPrice,
                    alert.CurrentEma,
                    alert.DistancePercent
                );
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "发送企业微信通知失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 发送企业微信通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 测试webhook推送按钮
        /// </summary>
        private async void BtnTestWebhook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_wechatService == null)
                {
                    MessageBox.Show("企业微信服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                btnTestWebhook.IsEnabled = false;
                btnTestWebhook.Content = "发送中...";

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🧪 开始测试企业微信推送...");
                var success = await _wechatService.SendTestMessageAsync();

                btnTestWebhook.IsEnabled = true;
                btnTestWebhook.Content = "🧪 测试推送";

                if (success)
                {
                    MessageBox.Show(
                        "测试消息已发送！\n\n" +
                        "请检查企业微信群是否收到消息。\n" +
                        "如果没有收到，可能的原因：\n" +
                        "1. webhook key已失效\n" +
                        "2. 机器人已被移除\n" +
                        "3. 群通知被关闭", 
                        "测试结果", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "测试消息发送失败！\n\n" +
                        "请检查控制台日志查看详细错误信息。", 
                        "测试失败", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                btnTestWebhook.IsEnabled = true;
                btnTestWebhook.Content = "🧪 测试推送";
                
                _logger?.LogError(ex, "测试webhook失败");
                MessageBox.Show($"测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 窗口关闭时保存配置
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isMonitoring)
            {
                BtnStop_Click(this, new RoutedEventArgs());
            }
            
            // 停止清理定时器
            _cleanupTimer?.Stop();
            _cleanupTimer = null;
            
            SaveConfig();
            base.OnClosing(e);
        }
    }

    /// <summary>
    /// 行号转换器 - 用于 DataGrid 显示行号
    /// </summary>
    public class RowNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is DataGridRow row)
            {
                var dataGrid = ItemsControl.ItemsControlFromItemContainer(row) as DataGrid;
                if (dataGrid != null)
                {
                    int index = dataGrid.ItemContainerGenerator.IndexFromContainer(row);
                    return (index + 1).ToString();
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

