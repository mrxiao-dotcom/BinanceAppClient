using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BinanceApps.Core.Interfaces;

namespace BinanceApps.WPF
{
    /// <summary>
    /// MultiTaskMonitorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MultiTaskMonitorWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MultiTaskMonitorWindow>? _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly Core.Services.HourlyEmaService? _hourlyEmaService;
        private readonly Core.Services.SupplyDataService? _supplyDataService;
        private readonly Core.Services.WeChatWebhookService? _wechatService;
        private readonly Core.Services.MultiPeriodKlineStorageService _klineStorageService;
        
        private ObservableCollection<MonitorTask> _tasks;
        private ObservableCollection<FilteredSymbol> _filteredSymbols;
        private MonitorTask? _selectedTask;
        
        // 任务调度相关
        private bool _isMonitoring = false;
        private CancellationTokenSource? _monitoringCts;
        private Task? _monitoringTask;
        
        // Webhook地址（可配置）
        private const string WEBHOOK_URL = "https://qyapi.weixin.qq.com/cgi-bin/webhook/send?key=e12bdda2-487f-4f78-972f-716d2ec45dd1";

        public MultiTaskMonitorWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger<MultiTaskMonitorWindow>>();
            _apiClient = _serviceProvider.GetRequiredService<IBinanceSimulatedApiClient>();
            
            // 获取服务（如果未注册则为null）
            _hourlyEmaService = _serviceProvider.GetService<Core.Services.HourlyEmaService>();
            _supplyDataService = _serviceProvider.GetService<Core.Services.SupplyDataService>();
            _wechatService = new Core.Services.WeChatWebhookService(WEBHOOK_URL, _logger);
            
            // 初始化多周期K线存储服务
            _klineStorageService = new Core.Services.MultiPeriodKlineStorageService(
                _apiClient,
                _serviceProvider.GetService<ILogger<Core.Services.MultiPeriodKlineStorageService>>());
            
            _tasks = new ObservableCollection<MonitorTask>();
            _filteredSymbols = new ObservableCollection<FilteredSymbol>();
            
            dgTasks.ItemsSource = _tasks;
            dgFilteredSymbols.ItemsSource = _filteredSymbols;
            
            // 加载已保存的范围
            LoadRanges();
            
            // 加载已保存的任务
            LoadTasks();
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 多任务监控窗口已初始化");
        }

        /// <summary>
        /// 加载已保存的范围到下拉框
        /// </summary>
        private void LoadRanges()
        {
            try
            {
                var ranges = RangeEditorWindow.LoadAllRanges();
                cmbRange.Items.Clear();
                
                foreach (var range in ranges)
                {
                    cmbRange.Items.Add(range.Name);
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 已加载 {ranges.Count} 个范围到下拉框");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载范围失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 加载范围失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建范围按钮点击事件
        /// </summary>
        private void BtnCreateRange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new RangeEditorWindow(_serviceProvider)
                {
                    Owner = this
                };
                
                if (window.ShowDialog() == true && !string.IsNullOrEmpty(window.SavedRangeName))
                {
                    // 重新加载范围列表
                    LoadRanges();
                    
                    // 选中新创建的范围
                    cmbRange.SelectedItem = window.SavedRangeName;
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 已创建并选中范围: {window.SavedRangeName}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建范围失败");
                MessageBox.Show($"创建范围失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 创建范围失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除范围按钮点击事件
        /// </summary>
        private void BtnDeleteRange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedRange = cmbRange.SelectedItem as string;
                if (string.IsNullOrEmpty(selectedRange))
                {
                    MessageBox.Show("请先选择要删除的范围", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"确定要删除范围 \"{selectedRange}\" 吗？", "确认删除", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    if (RangeEditorWindow.DeleteRange(selectedRange))
                    {
                        // 重新加载范围列表
                        LoadRanges();
                        
                        MessageBox.Show($"范围 \"{selectedRange}\" 已删除", "成功", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑️ 已删除范围: {selectedRange}");
                    }
                    else
                    {
                        MessageBox.Show("删除失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除范围失败");
                MessageBox.Show($"删除范围失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 删除范围失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 范围选择变化事件
        /// </summary>
        private void CmbRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                UpdateTaskName(sender, null);
                
                var selectedRange = cmbRange.SelectedItem as string;
                if (!string.IsNullOrEmpty(selectedRange))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📌 已选中范围: {selectedRange}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "范围选择变化处理失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 范围选择变化处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新任务名称（根据范围和所选条件自动生成）
        /// </summary>
        private void UpdateTaskName(object sender, EventArgs? e)
        {
            try
            {
                // 检查关键控件是否已初始化（窗口加载时可能会触发事件，但控件还未初始化）
                if (txtTaskName == null || cmbRange == null || 
                    rb1w == null || rb1d == null || rb2h == null || rb1h == null || 
                    rb30m == null || rb15m == null || rb5m == null)
                {
                    return;
                }
                
                var parts = new List<string>();
                
                // 1. 范围名称（必选）
                var selectedRange = cmbRange.SelectedItem as string;
                if (!string.IsNullOrEmpty(selectedRange))
                {
                    parts.Add(selectedRange);
                }
                
                // 2. 周期（必选）
                var period = GetSelectedPeriod();
                parts.Add(period);
                
                // 3. 价格范围（可选）
                if (chkPriceRange?.IsChecked == true)
                {
                    var days = txtPriceRangeDays?.Text?.Trim() ?? "30";
                    var min = txtPriceRangeMin?.Text?.Trim() ?? "0";
                    var max = txtPriceRangeMax?.Text?.Trim() ?? "100";
                    parts.Add($"价格{days}周期{min}-{max}");
                }
                
                // 4. 均线距离（可选）
                if (chkMaDistance?.IsChecked == true)
                {
                    var maPeriod = txtMaPeriod?.Text?.Trim() ?? "26";
                    var min = txtMaDistanceMin?.Text?.Trim() ?? "-10";
                    var max = txtMaDistanceMax?.Text?.Trim() ?? "10";
                    parts.Add($"{maPeriod}均线{min}-{max}");
                }
                
                // 5. 振幅（可选）
                if (chkAmplitude?.IsChecked == true)
                {
                    var days = txtAmplitudeDays?.Text?.Trim() ?? "30";
                    var min = txtAmplitudeMin?.Text?.Trim() ?? "0";
                    var max = txtAmplitudeMax?.Text?.Trim() ?? "50";
                    parts.Add($"振幅{days}周期{min}-{max}");
                }
                
                // 6. 流通市值（可选）
                if (chkMarketCap?.IsChecked == true)
                {
                    var min = txtMarketCapMin?.Text?.Trim() ?? "0";
                    var max = txtMarketCapMax?.Text?.Trim() ?? "999999";
                    parts.Add($"市值{min}-{max}万");
                }
                
                // 7. 24h成交额（可选）
                if (chkVolume24h?.IsChecked == true)
                {
                    var min = txtVolume24hMin?.Text?.Trim() ?? "1000";
                    var max = txtVolume24hMax?.Text?.Trim() ?? "999999";
                    parts.Add($"成交{min}-{max}万");
                }
                
                // 8. 流通率（可选）
                if (chkCirculationRate?.IsChecked == true)
                {
                    var min = txtCirculationRateMin?.Text?.Trim() ?? "0";
                    var max = txtCirculationRateMax?.Text?.Trim() ?? "100";
                    parts.Add($"流通率{min}-{max}");
                }
                
                // 9. 量比（可选）
                if (chkVolumeRatio?.IsChecked == true)
                {
                    var min = txtVolumeRatioMin?.Text?.Trim() ?? "0";
                    var max = txtVolumeRatioMax?.Text?.Trim() ?? "10";
                    parts.Add($"量比{min}-{max}");
                }
                
                // 拼接任务名称
                txtTaskName.Text = string.Join("", parts);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "更新任务名称失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 更新任务名称失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建任务按钮点击事件
        /// </summary>
        private void BtnCreateTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var taskName = txtTaskName.Text?.Trim();
                if (string.IsNullOrEmpty(taskName))
                {
                    MessageBox.Show("请输入任务名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建新任务
                var newTask = new MonitorTask
                {
                    TaskName = taskName,
                    Status = "待启动",
                    CreateTime = DateTime.Now,
                    // 收集所有参数
                    Parameters = new TaskParameters
                    {
                        // 范围（必选）
                        SelectedRange = cmbRange.SelectedItem?.ToString() ?? "",
                        
                        // 周期（必选）
                        Period = GetSelectedPeriod(),
                        
                        // 价格范围（可选）
                        EnablePriceRange = chkPriceRange.IsChecked == true,
                        PriceRangeDays = int.TryParse(txtPriceRangeDays.Text, out var prDays) ? prDays : 30,
                        PriceRangeMin = decimal.TryParse(txtPriceRangeMin.Text, out var prMin) ? prMin : 0,
                        PriceRangeMax = decimal.TryParse(txtPriceRangeMax.Text, out var prMax) ? prMax : 100,
                        
                        // 均线距离（可选）
                        EnableMaDistance = chkMaDistance.IsChecked == true,
                        MaPeriod = int.TryParse(txtMaPeriod.Text, out var maPeriod) ? maPeriod : 26,
                        MaDistanceMin = decimal.TryParse(txtMaDistanceMin.Text, out var maMin) ? maMin : -10,
                        MaDistanceMax = decimal.TryParse(txtMaDistanceMax.Text, out var maMax) ? maMax : 10,
                        
                        // 振幅（可选）
                        EnableAmplitude = chkAmplitude.IsChecked == true,
                        AmplitudeDays = int.TryParse(txtAmplitudeDays.Text, out var ampDays) ? ampDays : 30,
                        AmplitudeMin = decimal.TryParse(txtAmplitudeMin.Text, out var ampMin) ? ampMin : 0,
                        AmplitudeMax = decimal.TryParse(txtAmplitudeMax.Text, out var ampMax) ? ampMax : 50,
                        
                        // 流通市值（可选）
                        EnableMarketCap = chkMarketCap.IsChecked == true,
                        MarketCapMin = decimal.TryParse(txtMarketCapMin.Text, out var mcMin) ? mcMin : 0,
                        MarketCapMax = decimal.TryParse(txtMarketCapMax.Text, out var mcMax) ? mcMax : 999999,
                        
                        // 24h成交额（可选）
                        EnableVolume24h = chkVolume24h.IsChecked == true,
                        Volume24hMin = decimal.TryParse(txtVolume24hMin.Text, out var vol24Min) ? vol24Min : 1000,
                        Volume24hMax = decimal.TryParse(txtVolume24hMax.Text, out var vol24Max) ? vol24Max : 999999,
                        
                        // 流通率（可选）
                        EnableCirculationRate = chkCirculationRate.IsChecked == true,
                        CirculationRateMin = decimal.TryParse(txtCirculationRateMin.Text, out var crMin) ? crMin : 0,
                        CirculationRateMax = decimal.TryParse(txtCirculationRateMax.Text, out var crMax) ? crMax : 100,
                        
                        // 量比（可选）
                        EnableVolumeRatio = chkVolumeRatio.IsChecked == true,
                        VolumeRatioMin = decimal.TryParse(txtVolumeRatioMin.Text, out var vrMin) ? vrMin : 0,
                        VolumeRatioMax = decimal.TryParse(txtVolumeRatioMax.Text, out var vrMax) ? vrMax : 10,
                        
                        // 监控频率（必选，多选）
                        EnableRealtime = chkRealtime.IsChecked == true,
                        EnableInterval = chkInterval.IsChecked == true,
                        MonitorIntervalMinutes = int.TryParse(txtMonitorInterval.Text, out var interval) ? interval : 5
                    }
                };

                _tasks.Add(newTask);
                SaveTasks(); // 保存到本地文件
                txtTaskName.Clear();
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 已创建任务: {taskName}");
                MessageBox.Show($"任务 \"{taskName}\" 已创建", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "创建任务失败");
                MessageBox.Show($"创建任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取选中的周期
        /// </summary>
        private string GetSelectedPeriod()
        {
            if (rb1w.IsChecked == true) return "1w";
            if (rb1d.IsChecked == true) return "1d";
            if (rb2h.IsChecked == true) return "2h";
            if (rb1h.IsChecked == true) return "1h";
            if (rb30m.IsChecked == true) return "30m";
            if (rb15m.IsChecked == true) return "15m";
            if (rb5m.IsChecked == true) return "5m";
            return "1h";
        }

        /// <summary>
        /// 任务列表选择变化事件
        /// </summary>
        private void DgTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTasks.SelectedItem is MonitorTask selectedTask)
            {
                _selectedTask = selectedTask;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📌 已选中任务: {selectedTask.TaskName}");
                
                // 加载并显示该任务的筛选结果
                LoadFilteredSymbols(selectedTask);
            }
        }

        /// <summary>
        /// 加载筛选后的合约列表
        /// </summary>
        private void LoadFilteredSymbols(MonitorTask task)
        {
            try
            {
                _filteredSymbols.Clear();
                
                // 只要任务有结果，就显示（不管任务状态）
                if (task.Results != null && task.Results.Count > 0)
                {
                    foreach (var result in task.Results)
                    {
                        _filteredSymbols.Add(result);
                    }
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📊 已加载任务 [{task.TaskName}] 的结果，共 {_filteredSymbols.Count} 个合约 (状态: {task.Status})");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ️ 任务 [{task.TaskName}] 暂无结果 (状态: {task.Status})");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载筛选结果失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 加载筛选结果失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动监控按钮点击事件
        /// </summary>
        private void BtnStartMonitor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isMonitoring)
                {
                    MessageBox.Show("监控已在运行中", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取所有启用的任务
                var enabledTasks = _tasks.Where(t => t.IsEnabled).ToList();
                if (enabledTasks.Count == 0)
                {
                    MessageBox.Show("没有启用的任务，请先勾选要执行的任务", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _isMonitoring = true;
                _monitoringCts = new CancellationTokenSource();
                
                btnStartMonitor.IsEnabled = false;
                btnStopMonitor.IsEnabled = true;
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ▶ 开始监控，已启用任务数: {enabledTasks.Count}");
                
                // 在后台线程中执行监控
                _monitoringTask = Task.Run(async () => await ExecuteMonitoringLoopAsync(enabledTasks, _monitoringCts.Token));
                _ = _monitoringTask; // 火忘式（fire-and-forget）
                
                MessageBox.Show($"已启动监控，共 {enabledTasks.Count} 个任务", "成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "启动监控失败");
                MessageBox.Show($"启动监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _isMonitoring = false;
                btnStartMonitor.IsEnabled = true;
                btnStopMonitor.IsEnabled = false;
            }
        }

        /// <summary>
        /// 停止监控按钮点击事件
        /// </summary>
        private async void BtnStopMonitor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isMonitoring)
                {
                    MessageBox.Show("监控未在运行", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏸ 停止监控...");
                
                _monitoringCts?.Cancel();
                
                // 等待监控任务完成
                if (_monitoringTask != null)
                {
                    try
                    {
                        await _monitoringTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消
                    }
                }
                
                _isMonitoring = false;
                btnStartMonitor.IsEnabled = true;
                btnStopMonitor.IsEnabled = false;
                
                // 更新所有任务状态
                foreach (var task in _tasks.Where(t => t.Status == "正在执行" || t.Status == "等待执行"))
                {
                    task.Status = "已停止";
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 监控已停止");
                MessageBox.Show("监控已停止", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "停止监控失败");
                MessageBox.Show($"停止监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 监控循环主逻辑
        /// </summary>
        private async Task ExecuteMonitoringLoopAsync(List<MonitorTask> tasks, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 进入监控循环");
                
                // 初始化任务执行时间
                var now = DateTime.Now;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🎯 开始初始化 {tasks.Count} 个任务的执行时间");
                
                foreach (var task in tasks)
                {
                    if (task.Parameters == null)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 任务 [{task.TaskName}] 参数为空，跳过");
                        continue;
                    }
                    
                    // 如果勾选了即时执行，设置为立即执行
                    if (task.Parameters.EnableRealtime)
                    {
                        task.NextExecutionTime = now;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 任务 [{task.TaskName}] 设置为即时执行: {now:HH:mm:ss}");
                    }
                    else if (task.Parameters.EnableInterval)
                    {
                        task.NextExecutionTime = now.AddMinutes(task.Parameters.MonitorIntervalMinutes);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏰ 任务 [{task.TaskName}] 设置为定时执行: {task.NextExecutionTime:HH:mm:ss} (间隔 {task.Parameters.MonitorIntervalMinutes} 分钟)");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 任务 [{task.TaskName}] 未勾选任何执行方式，跳过");
                        task.NextExecutionTime = null;
                    }
                    
                    await Dispatcher.InvokeAsync(() => task.Status = "等待执行");
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 任务初始化完成，开始主循环");
                Console.WriteLine();
                
                // 主循环
                while (!cancellationToken.IsCancellationRequested)
                {
                    now = DateTime.Now;
                    
                    // 查找需要执行的任务
                    var tasksToExecute = tasks
                        .Where(t => t.IsEnabled && t.NextExecutionTime.HasValue && t.NextExecutionTime.Value <= now)
                        .OrderBy(t => t.NextExecutionTime)
                        .ToList();
                    
                    // 输出当前状态（每10秒输出一次）
                    var enabledTasks = tasks.Where(t => t.IsEnabled).ToList();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔍 主循环检查: 启用任务 {enabledTasks.Count} 个，待执行 {tasksToExecute.Count} 个");
                    
                    foreach (var task in enabledTasks)
                    {
                        var nextTimeStr = task.NextExecutionTime.HasValue 
                            ? task.NextExecutionTime.Value.ToString("HH:mm:ss") 
                            : "无";
                        var willExecute = tasksToExecute.Contains(task) ? "✅" : "⏳";
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}]   {willExecute} [{task.TaskName}] 下次执行: {nextTimeStr}, 状态: {task.Status}");
                    }
                    Console.WriteLine();
                    
                    if (tasksToExecute.Count > 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚀 开始执行 {tasksToExecute.Count} 个任务");
                        
                        foreach (var task in tasksToExecute)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ▶ 执行任务: [{task.TaskName}]");
                            await ExecuteTaskAsync(task, cancellationToken);
                            
                            // 计算下次执行时间
                            if (task.Parameters != null && task.Parameters.EnableInterval)
                            {
                                // 定时任务：设置下次执行时间，清除完成时间（因为任务还会继续执行）
                                task.NextExecutionTime = DateTime.Now.AddMinutes(task.Parameters.MonitorIntervalMinutes);
                                await Dispatcher.InvokeAsync(() => 
                                {
                                    task.Status = "等待执行";
                                    task.CompletedTime = null; // 清除完成时间，因为任务还会继续执行
                                });
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏰ 任务 [{task.TaskName}] 下次执行: {task.NextExecutionTime:HH:mm:ss}");
                            }
                            else
                            {
                                // 即时任务：只执行一次，保持"已完成"状态和完成时间
                                task.NextExecutionTime = null;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⏹ 任务 [{task.TaskName}] 即时任务已完成，不再执行");
                            }
                        }
                        
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 批次任务执行完成");
                        Console.WriteLine();
                    }
                    
                    // 等待一段时间再检查
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 监控循环已取消");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "监控循环异常");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 监控循环异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行单个任务
        /// </summary>
        private async Task ExecuteTaskAsync(MonitorTask task, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🚀 开始执行任务: {task.TaskName}");
                await Dispatcher.InvokeAsync(() => task.Status = "正在执行");
                
                if (task.Parameters == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 任务参数为空");
                    return;
                }
                
                // 输出缓存统计信息
                var (cacheCount, totalKlines) = _klineStorageService.GetCacheStats();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💾 当前缓存: {cacheCount} 个品种，共 {totalKlines} 根K线");
                
                // TODO: 步骤1 - 获取范围内的合约列表
                var symbols = await GetSymbolsFromRangeAsync(task.Parameters.SelectedRange);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 获取到 {symbols.Count} 个合约");
                
                // TODO: 步骤2 - 根据周期获取K线数据
                // 需要实现：从本地文件读取，如不够新则从交易所补充
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📊 开始获取K线数据，周期: {task.Parameters.Period}");
                
                // TODO: 步骤3 - 计算每个合约的指标
                var results = new List<FilteredSymbol>();
                foreach (var symbol in symbols)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    // TODO: 计算位置、振幅、EMA距离、市值等指标
                    var filtered = await CalculateSymbolIndicatorsAsync(symbol, task.Parameters);
                    
                    if (filtered != null)
                    {
                        results.Add(filtered);
                    }
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔢 完成指标计算，共 {results.Count} 个合约");
                
                // 输出缓存统计信息（执行后）
                var (cacheCountAfter, totalKlinesAfter) = _klineStorageService.GetCacheStats();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💾 执行后缓存: {cacheCountAfter} 个品种，共 {totalKlinesAfter} 根K线");
                
                // TODO: 步骤4 - 根据任务参数筛选
                var filteredResults = FilterByParameters(results, task.Parameters);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 筛选完成，符合条件的合约数: {filteredResults.Count}");
                
                // 保存结果到任务
                task.Results = filteredResults;
                await Dispatcher.InvokeAsync(() =>
                {
                    task.Status = "已完成";
                    task.CompletedTime = DateTime.Now;
                    task.SymbolCount = filteredResults.Count;  // 更新查询合约数
                });
                
                // TODO: 步骤5 - 发送Webhook通知
                if (filteredResults.Count > 0)
                {
                    await SendWebhookNotificationAsync(task.TaskName, filteredResults);
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🎉 任务执行完成: {task.TaskName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"执行任务失败: {task.TaskName}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 执行任务失败: {task.TaskName}, {ex.Message}");
                await Dispatcher.InvokeAsync(() => task.Status = "执行失败");
            }
        }

        /// <summary>
        /// 从范围获取合约列表
        /// </summary>
        private async Task<List<string>> GetSymbolsFromRangeAsync(string rangeName)
        {
            try
            {
                var ranges = RangeEditorWindow.LoadAllRanges();
                var range = ranges.FirstOrDefault(r => r.Name == rangeName);
                
                if (range != null && range.Symbols != null)
                {
                    return await Task.FromResult(range.Symbols);
                }
                
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "获取范围合约失败");
                return new List<string>();
            }
        }

        /// <summary>
        /// 计算单个合约的指标
        /// </summary>
        private async Task<FilteredSymbol?> CalculateSymbolIndicatorsAsync(string symbol, TaskParameters parameters)
        {
            try
            {
                // 步骤1: 获取K线数据
                var klines = await GetKlineDataAsync(symbol, parameters.Period, 
                    Math.Max(parameters.PriceRangeDays, Math.Max(parameters.AmplitudeDays, parameters.MaPeriod + 50)));
                
                if (klines == null || klines.Count == 0)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ {symbol} 无K线数据");
                    return null;
                }
                
                var lastKline = klines.Last();
                var lastPrice = lastKline.ClosePrice;
                
                // 步骤2: 计算位置（价格范围）
                decimal position = 0;
                if (parameters.EnablePriceRange && parameters.PriceRangeDays > 0)
                {
                    var priceKlines = klines.TakeLast(parameters.PriceRangeDays).ToList();
                    var highPrice = priceKlines.Max(k => k.HighPrice);
                    var lowPrice = priceKlines.Min(k => k.LowPrice);
                    
                    if (highPrice > lowPrice)
                    {
                        position = (lastPrice - lowPrice) / (highPrice - lowPrice) * 100;
                    }
                }
                
                // 步骤3: 计算振幅
                decimal amplitude = 0;
                if (parameters.EnableAmplitude && parameters.AmplitudeDays > 0)
                {
                    var ampKlines = klines.TakeLast(parameters.AmplitudeDays).ToList();
                    var highPrice = ampKlines.Max(k => k.HighPrice);
                    var lowPrice = ampKlines.Min(k => k.LowPrice);
                    
                    if (lowPrice > 0)
                    {
                        amplitude = (highPrice - lowPrice) / lowPrice * 100;
                    }
                }
                
                // 步骤4: 计算EMA距离
                decimal emaDistance = 0;
                if (parameters.EnableMaDistance && parameters.MaPeriod > 0)
                {
                    var emaValues = CalculateEMA(klines, parameters.MaPeriod);
                    if (emaValues.Count > 0)
                    {
                        var emaValue = emaValues.Last();
                        if (emaValue > 0)
                        {
                            emaDistance = (lastPrice - emaValue) / emaValue * 100;
                        }
                    }
                }
                
                // 步骤5: 获取24h成交额（从ticker）
                decimal volume24h = 0;
                try
                {
                    var ticker = await _apiClient.Get24hrPriceStatisticsAsync(symbol);
                    volume24h = ticker.QuoteVolume / 10000; // 转换为万USDT
                }
                catch
                {
                    // 忽略ticker获取失败
                }
                
                // 步骤6: 计算流通市值
                decimal marketCap = 0;
                decimal circulationRate = 0;
                if (_supplyDataService != null)
                {
                    var supply = _supplyDataService.GetSupplyData(symbol);
                    if (supply != null && supply.CirculatingSupply > 0)
                    {
                        marketCap = supply.CirculatingSupply * lastPrice / 10000; // 万USDT
                        
                        // 计算流通率
                        if (supply.TotalSupply > 0)
                        {
                            circulationRate = supply.CirculatingSupply / supply.TotalSupply * 100;
                        }
                    }
                }
                
                // 步骤7: 计算量比
                decimal volumeRatio = 0;
                if (marketCap > 0 && volume24h > 0)
                {
                    volumeRatio = volume24h / marketCap;
                }
                
                return new FilteredSymbol
                {
                    Symbol = symbol,
                    LastPrice = lastPrice,
                    Position = position,
                    Amplitude = amplitude,
                    EmaDistance = emaDistance,
                    Volume24h = volume24h,
                    MarketCap = marketCap,
                    CirculationRate = circulationRate,
                    VolumeRatio = volumeRatio,
                    UpdateTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"计算合约指标失败: {symbol}");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 计算 {symbol} 指标失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 获取K线数据（增量获取，优先使用本地缓存）
        /// </summary>
        private async Task<List<Core.Models.Kline>> GetKlineDataAsync(string symbol, string period, int limit)
        {
            try
            {
                // 使用多周期K线存储服务的增量获取功能
                // 自动处理：本地加载 -> 判断是否需要更新 -> 增量下载 -> 合并保存
                var klines = await _klineStorageService.GetKlineDataWithIncrementalUpdateAsync(symbol, period, limit);
                
                return klines ?? new List<Core.Models.Kline>();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"获取K线数据失败: {symbol} ({period})");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 获取K线数据失败: {symbol} ({period}): {ex.Message}");
                return new List<Core.Models.Kline>();
            }
        }
        
        /// <summary>
        /// 计算EMA（指数移动平均）
        /// </summary>
        private List<decimal> CalculateEMA(List<Core.Models.Kline> klines, int period)
        {
            var emaValues = new List<decimal>();
            if (klines.Count < period) return emaValues;
            
            decimal multiplier = 2m / (period + 1);
            
            // 初始EMA = 前N个的简单平均
            decimal sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += klines[i].ClosePrice;
            }
            decimal ema = sum / period;
            emaValues.Add(ema);
            
            // 后续EMA = (当前价格 - 前一个EMA) * 乘数 + 前一个EMA
            for (int i = period; i < klines.Count; i++)
            {
                ema = (klines[i].ClosePrice - ema) * multiplier + ema;
                emaValues.Add(ema);
            }
            
            return emaValues;
        }

        /// <summary>
        /// 根据参数筛选结果
        /// </summary>
        private List<FilteredSymbol> FilterByParameters(List<FilteredSymbol> results, TaskParameters parameters)
        {
            try
            {
                var filtered = results.AsEnumerable();
                
                // 价格范围筛选
                if (parameters.EnablePriceRange)
                {
                    filtered = filtered.Where(r => 
                        r.Position >= parameters.PriceRangeMin && 
                        r.Position <= parameters.PriceRangeMax);
                }
                
                // 均线距离筛选
                if (parameters.EnableMaDistance)
                {
                    filtered = filtered.Where(r => 
                        r.EmaDistance >= parameters.MaDistanceMin && 
                        r.EmaDistance <= parameters.MaDistanceMax);
                }
                
                // 振幅筛选
                if (parameters.EnableAmplitude)
                {
                    filtered = filtered.Where(r => 
                        r.Amplitude >= parameters.AmplitudeMin && 
                        r.Amplitude <= parameters.AmplitudeMax);
                }
                
                // 流通市值筛选
                if (parameters.EnableMarketCap)
                {
                    filtered = filtered.Where(r => 
                        r.MarketCap >= parameters.MarketCapMin && 
                        r.MarketCap <= parameters.MarketCapMax);
                }
                
                // 24h成交额筛选
                if (parameters.EnableVolume24h)
                {
                    filtered = filtered.Where(r => 
                        r.Volume24h >= parameters.Volume24hMin && 
                        r.Volume24h <= parameters.Volume24hMax);
                }
                
                // 流通率筛选
                if (parameters.EnableCirculationRate)
                {
                    filtered = filtered.Where(r => 
                        r.CirculationRate >= parameters.CirculationRateMin && 
                        r.CirculationRate <= parameters.CirculationRateMax);
                }
                
                // 量比筛选
                if (parameters.EnableVolumeRatio)
                {
                    filtered = filtered.Where(r => 
                        r.VolumeRatio >= parameters.VolumeRatioMin && 
                        r.VolumeRatio <= parameters.VolumeRatioMax);
                }
                
                return filtered.ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "筛选结果失败");
                return results;
            }
        }

        /// <summary>
        /// 发送Webhook通知
        /// </summary>
        private async Task SendWebhookNotificationAsync(string taskName, List<FilteredSymbol> results)
        {
            try
            {
                if (_wechatService == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ WeChat服务未初始化");
                    return;
                }
                
                // 构建消息内容
                var message = $"🎯 多任务监控提醒\n\n";
                message += $"任务: {taskName}\n";
                message += $"完成时间: {DateTime.Now:MM-dd HH:mm:ss}\n";
                message += $"符合条件: {results.Count} 个合约\n";
                message += $"━━━━━━━━━━━━━━\n\n";
                
                // 显示前10个结果
                var displayResults = results.Take(10).ToList();
                foreach (var result in displayResults)
                {
                    message += $"📊 {result.Symbol}\n";
                    message += $"  价格: {result.LastPrice:F4}\n";
                    
                    if (result.Position > 0)
                        message += $"  位置: {result.Position:F2}%\n";
                    
                    if (result.Amplitude > 0)
                        message += $"  振幅: {result.Amplitude:F2}%\n";
                    
                    if (result.EmaDistance != 0)
                        message += $"  EMA距离: {result.EmaDistance:F2}%\n";
                    
                    if (result.Volume24h > 0)
                        message += $"  24h成交: {result.Volume24h:F0}万\n";
                    
                    if (result.MarketCap > 0)
                        message += $"  市值: {result.MarketCap:F0}万\n";
                    
                    if (result.VolumeRatio > 0)
                        message += $"  量比: {result.VolumeRatio:F2}\n";
                    
                    message += "\n";
                }
                
                if (results.Count > 10)
                {
                    message += $"... 还有 {results.Count - 10} 个合约";
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📤 准备发送Webhook通知，消息长度: {message.Length}");
                
                // 发送到企业微信
                var success = await _wechatService.SendTextMessageAsync(message, mentionAll: true);
                
                if (success)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ Webhook通知发送成功");
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ Webhook通知发送失败");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "发送Webhook通知失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 发送Webhook通知异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存任务到本地文件
        /// </summary>
        private void SaveTasks()
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BinanceApps", "Tasks");
                Directory.CreateDirectory(appDataPath);
                
                var filePath = Path.Combine(appDataPath, "monitor_tasks.json");
                
                // 只保存必要的数据（排除运行时状态）
                var tasksToSave = _tasks.Select(t => new
                {
                    t.TaskName,
                    t.IsEnabled,
                    t.CreateTime,
                    Parameters = t.Parameters
                }).ToList();
                
                var json = JsonSerializer.Serialize(tasksToSave, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                File.WriteAllText(filePath, json);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 💾 任务已保存: {_tasks.Count} 个");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存任务失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 保存任务失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从本地文件加载任务
        /// </summary>
        private void LoadTasks()
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BinanceApps", "Tasks");
                var filePath = Path.Combine(appDataPath, "monitor_tasks.json");
                
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ℹ️ 未找到任务配置文件");
                    return;
                }
                
                var json = File.ReadAllText(filePath);
                var tasks = JsonSerializer.Deserialize<List<JsonElement>>(json);
                
                if (tasks != null)
                {
                    foreach (var taskJson in tasks)
                    {
                        try
                        {
                            var task = new MonitorTask
                            {
                                TaskName = taskJson.GetProperty("TaskName").GetString() ?? "",
                                IsEnabled = taskJson.GetProperty("IsEnabled").GetBoolean(),
                                CreateTime = taskJson.GetProperty("CreateTime").GetDateTime(),
                                Status = "待启动",
                                CompletedTime = null,  // 清除运行时状态
                                NextExecutionTime = null,  // 清除运行时状态
                                SymbolCount = 0  // 清除查询合约数
                            };
                            
                            if (taskJson.TryGetProperty("Parameters", out var parametersJson))
                            {
                                task.Parameters = JsonSerializer.Deserialize<TaskParameters>(parametersJson.GetRawText());
                            }
                            
                            _tasks.Add(task);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 加载任务失败: {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📂 已加载 {_tasks.Count} 个任务");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载任务失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 加载任务失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 筛选结果列表双击事件（复制合约名到剪贴板）
        /// </summary>
        private void DgFilteredSymbols_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (dgFilteredSymbols.SelectedItem is FilteredSymbol symbol)
                {
                    CopySymbolToClipboard(symbol.Symbol);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "复制合约名失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 复制失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 复制合约名到剪贴板（带重试机制）
        /// </summary>
        private void CopySymbolToClipboard(string symbol)
        {
            try
            {
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
                        attempts++;
                        if (attempts < maxAttempts)
                        {
                            System.Threading.Thread.Sleep(100);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 剪贴板被占用，正在重试... ({attempts}/{maxAttempts})");
                        }
                    }
                }
                
                if (!success)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 无法访问剪贴板，请手动复制: {symbol}");
                    MessageBox.Show($"无法访问剪贴板\n请手动复制: {symbol}", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "复制合约名失败");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 复制失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 删除任务按钮点击事件
        /// </summary>
        private void BtnDeleteTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedTask == null)
                {
                    MessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"确定要删除任务 \"{_selectedTask.TaskName}\" 吗？", "确认", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _tasks.Remove(_selectedTask);
                    SaveTasks(); // 保存到本地文件
                    _filteredSymbols.Clear();
                    _selectedTask = null;
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑 任务已删除");
                    MessageBox.Show("任务已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "删除任务失败");
                MessageBox.Show($"删除任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// 监控任务模型
    /// </summary>
    public class MonitorTask : INotifyPropertyChanged
    {
        private string _status = "待启动";
        private bool _isEnabled = true;
        private DateTime? _completedTime;
        private DateTime? _nextExecutionTime;
        
        public string TaskName { get; set; } = "";
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }
        
        public string Status 
        { 
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }
        
        public DateTime CreateTime { get; set; }
        
        public DateTime? CompletedTime
        {
            get => _completedTime;
            set
            {
                if (_completedTime != value)
                {
                    _completedTime = value;
                    OnPropertyChanged(nameof(CompletedTime));
                }
            }
        }
        
        public TaskParameters? Parameters { get; set; }
        
        // 任务执行结果缓存
        public List<FilteredSymbol> Results { get; set; } = new();
        
        // 下次执行时间
        public DateTime? NextExecutionTime
        {
            get => _nextExecutionTime;
            set
            {
                if (_nextExecutionTime != value)
                {
                    _nextExecutionTime = value;
                    OnPropertyChanged(nameof(NextExecutionTime));
                }
            }
        }
        
        // 查询到的合约数量
        private int _symbolCount = 0;
        public int SymbolCount
        {
            get => _symbolCount;
            set
            {
                if (_symbolCount != value)
                {
                    _symbolCount = value;
                    OnPropertyChanged(nameof(SymbolCount));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 任务参数模型
    /// </summary>
    public class TaskParameters
    {
        // 范围（必选）
        public string SelectedRange { get; set; } = "";
        
        // 周期（必选）
        public string Period { get; set; } = "1h";
        
        // 价格范围（可选）
        public bool EnablePriceRange { get; set; }
        public int PriceRangeDays { get; set; }
        public decimal PriceRangeMin { get; set; }
        public decimal PriceRangeMax { get; set; }
        
        // 均线距离（可选）
        public bool EnableMaDistance { get; set; }
        public int MaPeriod { get; set; }
        public decimal MaDistanceMin { get; set; }
        public decimal MaDistanceMax { get; set; }
        
        // 振幅（可选）
        public bool EnableAmplitude { get; set; }
        public int AmplitudeDays { get; set; }
        public decimal AmplitudeMin { get; set; }
        public decimal AmplitudeMax { get; set; }
        
        // 流通市值（可选）
        public bool EnableMarketCap { get; set; }
        public decimal MarketCapMin { get; set; }
        public decimal MarketCapMax { get; set; }
        
        // 24h成交额（可选）
        public bool EnableVolume24h { get; set; }
        public decimal Volume24hMin { get; set; }
        public decimal Volume24hMax { get; set; }
        
        // 流通率（可选）
        public bool EnableCirculationRate { get; set; }
        public decimal CirculationRateMin { get; set; }
        public decimal CirculationRateMax { get; set; }
        
        // 量比（可选）
        public bool EnableVolumeRatio { get; set; }
        public decimal VolumeRatioMin { get; set; }
        public decimal VolumeRatioMax { get; set; }
        
        // 监控频率（必选，多选）
        public bool EnableRealtime { get; set; }
        public bool EnableInterval { get; set; }
        public int MonitorIntervalMinutes { get; set; }
    }

    /// <summary>
    /// 筛选后的合约模型
    /// </summary>
    public class FilteredSymbol
    {
        public string Symbol { get; set; } = "";
        public decimal LastPrice { get; set; }
        
        // 位置百分比
        public decimal Position { get; set; }
        
        // 振幅百分比
        public decimal Amplitude { get; set; }
        
        // EMA距离百分比
        public decimal EmaDistance { get; set; }
        
        // 24h成交额（万USDT）
        public decimal Volume24h { get; set; }
        
        // 流通市值（万USDT）
        public decimal MarketCap { get; set; }
        
        // 流通率（百分比）
        public decimal CirculationRate { get; set; }
        
        // 量比
        public decimal VolumeRatio { get; set; }
        
        public DateTime UpdateTime { get; set; }
    }
}

