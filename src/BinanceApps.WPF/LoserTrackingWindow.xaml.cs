using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace BinanceApps.WPF
{
    public partial class LoserTrackingWindow : Window
    {
        private readonly ILogger<LoserTrackingWindow> _logger;
        private readonly LoserTrackingService _loserService;
        
        private DispatcherTimer? _countdownTimer;
        private LoserTrackingConfig _config = new();
        
        private List<LoserContract> _realtimeLosers = new();
        private Dictionary<string, CachedLoserContract> _cachedContracts = new();
        private Dictionary<string, RecycledLoserContract> _recycledContracts = new();
        
        private bool _isMonitoring = false;
        private bool _isScanning = false;
        private DateTime _nextScanTime;
        
        private readonly object _dataLock = new object();
        private readonly string _instanceId = "default";
        private static int _windowCounter = 0;
        private readonly int _windowNumber;
        
        public LoserTrackingWindow(ILogger<LoserTrackingWindow> logger, LoserTrackingService loserService)
        {
            InitializeComponent();
            
            _logger = logger;
            _loserService = loserService;
            
            _windowNumber = ++_windowCounter;
            Title = $"近期跌幅榜追踪 - 窗口{_windowNumber}";
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 加载持久化数据
                await LoadDataAsync();
                
                // 更新UI
                UpdateConfigUI();
                RefreshAllDataGrids();
                
                _logger.LogInformation($"跌幅榜追踪窗口{_windowNumber}已加载");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "窗口加载时发生错误");
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isMonitoring)
            {
                StopMonitoring();
            }
            
            // 保存数据
            await SaveDataAsync();
            
            _logger.LogInformation($"跌幅榜追踪窗口{_windowNumber}已关闭");
        }
        
        private async void BtnToggleMonitoring_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoring)
            {
                await StartMonitoringAsync();
            }
            else
            {
                StopMonitoring();
            }
        }
        
        private async System.Threading.Tasks.Task StartMonitoringAsync()
        {
            try
            {
                // 从UI读取配置
                ReadConfigFromUI();
                
                _isMonitoring = true;
                btnToggleMonitoring.Content = "停止监控";
                btnToggleMonitoring.Style = (Style)FindResource("StopButtonStyle");
                
                // 启动倒计时定时器（每秒更新）
                _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _countdownTimer.Tick += CountdownTimer_Tick;
                _countdownTimer.Start();
                
                // 立即执行第一次扫描
                await ScanLosersAsync();
                
                _logger.LogInformation($"跌幅榜追踪监控已启动（窗口{_windowNumber}）");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监控失败");
                MessageBox.Show($"启动监控失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                StopMonitoring();
            }
        }
        
        private void StopMonitoring()
        {
            _isMonitoring = false;
            btnToggleMonitoring.Content = "启动监控";
            btnToggleMonitoring.Style = (Style)FindResource("ButtonStyle");
            
            _countdownTimer?.Stop();
            _countdownTimer = null;
            
            txtStatus.Text = "已停止";
            txtNextScan.Text = string.Empty;
            
            _logger.LogInformation($"跌幅榜追踪监控已停止（窗口{_windowNumber}）");
        }
        
        private async void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isMonitoring) return;
            
            var now = DateTime.Now;
            
            // 更新倒计时显示
            if (_nextScanTime > now)
            {
                var remaining = (_nextScanTime - now).TotalSeconds;
                txtNextScan.Text = $"下次扫描: {remaining:F0}秒";
            }
            else
            {
                txtNextScan.Text = string.Empty;
            }
            
            // 检查是否需要扫描
            if (now >= _nextScanTime && !_isScanning)
            {
                await ScanLosersAsync();
            }
        }
        
        private async System.Threading.Tasks.Task ScanLosersAsync()
        {
            if (_isScanning) return;
            
            _isScanning = true;
            
            try
            {
                txtStatus.Text = "扫描中...";
                
                // 1. 后台线程扫描跌幅榜（不需要锁）
                var losers = await _loserService.ScanTopLosersAsync(_config);
                
                // 2. 加锁更新共享数据
                lock (_dataLock)
                {
                    _realtimeLosers = losers;
                }
                
                // 3. 后台更新缓存数据
                await _loserService.UpdateCachedContractsAsync(_realtimeLosers, _cachedContracts, _config);
                
                // 4. 清理过期缓存
                _loserService.CleanExpiredCache(_cachedContracts, _recycledContracts);
                _loserService.CleanRecycledContracts(_recycledContracts);
                
                // 5. UI线程刷新界面
                await Dispatcher.InvokeAsync(() =>
                {
                    lock (_dataLock)
                    {
                        RefreshAllDataGrids();
                    }
                    
                    // 更新状态
                    _nextScanTime = DateTime.Now.AddSeconds(_config.ScanIntervalSeconds);
                    txtStatus.Text = $"监控中 - 实时榜:{_realtimeLosers.Count} | 缓存:{_cachedContracts.Count}";
                });
                
                // 6. 保存数据
                await SaveDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描跌幅榜时发生错误");
                await Dispatcher.InvokeAsync(() =>
                {
                    txtStatus.Text = $"扫描失败: {ex.Message}";
                });
            }
            finally
            {
                _isScanning = false;
            }
        }
        
        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            var data = await _loserService.LoadDataAsync(_instanceId);
            
            if (data != null)
            {
                _config = data.Config;
                _cachedContracts = data.CachedContracts ?? new();
                _recycledContracts = data.RecycledContracts ?? new();
                
                _logger.LogInformation($"已加载跌幅榜数据: 缓存={_cachedContracts.Count}, 回收={_recycledContracts.Count}");
            }
        }
        
        private async System.Threading.Tasks.Task SaveDataAsync()
        {
            var data = new LoserTrackingData
            {
                Config = _config,
                CachedContracts = _cachedContracts,
                RecycledContracts = _recycledContracts
            };
            
            await _loserService.SaveDataAsync(_instanceId, data);
        }
        
        private void UpdateConfigUI()
        {
            txtNDays.Text = _config.NDays.ToString();
            txtTopCount.Text = _config.TopCount.ToString();
            txtReboundZone1.Text = _config.ReboundZone1Threshold.ToString("F0");
            txtReboundZone2.Text = _config.ReboundZone2Threshold.ToString("F0");
            txtScanInterval.Text = _config.ScanIntervalSeconds.ToString();
            txtCacheExpiry.Text = _config.CacheExpiryHours.ToString();
        }
        
        private void ReadConfigFromUI()
        {
            _config.NDays = int.TryParse(txtNDays.Text, out int nDays) ? nDays : 30;
            _config.TopCount = int.TryParse(txtTopCount.Text, out int topCount) ? topCount : 30;
            _config.ReboundZone1Threshold = decimal.TryParse(txtReboundZone1.Text, out decimal rebound1) ? rebound1 : 10m;
            _config.ReboundZone2Threshold = decimal.TryParse(txtReboundZone2.Text, out decimal rebound2) ? rebound2 : 20m;
            _config.ScanIntervalSeconds = int.TryParse(txtScanInterval.Text, out int interval) ? interval : 5;
            _config.CacheExpiryHours = int.TryParse(txtCacheExpiry.Text, out int cacheHours) ? cacheHours : 240;
        }
        
        private void RefreshAllDataGrids()
        {
            // 1. 实时跌幅榜
            dgRealtime.ItemsSource = _realtimeLosers.OrderBy(l => l.Rank).ToList();
            txtRealtimeCount.Text = $"({_realtimeLosers.Count}个)";
            
            // 2. 缓存区
            dgCached.ItemsSource = _cachedContracts.Values
                .OrderByDescending(c => c.EntryTime)
                .ToList();
            txtCachedCount.Text = $"({_cachedContracts.Count}个)";
            
            // 3. 反弹一区（反弹幅度 >= 阈值1）
            var rebound1List = _cachedContracts.Values
                .Where(c => c.CurrentReboundPercent >= _config.ReboundZone1Threshold)
                .OrderByDescending(c => c.CurrentReboundPercent)
                .ToList();
            dgRebound1.ItemsSource = rebound1List;
            txtRebound1Count.Text = $"({rebound1List.Count}个)";
            
            // 4. 反弹二区（反弹幅度 >= 阈值2）
            var rebound2List = _cachedContracts.Values
                .Where(c => c.CurrentReboundPercent >= _config.ReboundZone2Threshold)
                .OrderByDescending(c => c.CurrentReboundPercent)
                .ToList();
            dgRebound2.ItemsSource = rebound2List;
            txtRebound2Count.Text = $"({rebound2List.Count}个)";
            
            // 5. 回收区
            dgRecycled.ItemsSource = _recycledContracts.Values
                .OrderByDescending(r => r.RecycleTime)
                .ToList();
            txtRecycledCount.Text = $"({_recycledContracts.Count}个)";
        }
        
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                string symbol = string.Empty;
                
                if (dataGrid.SelectedItem is LoserContract loser)
                {
                    symbol = loser.Symbol;
                }
                else if (dataGrid.SelectedItem is CachedLoserContract cached)
                {
                    symbol = cached.Symbol;
                }
                else if (dataGrid.SelectedItem is RecycledLoserContract recycled)
                {
                    symbol = recycled.Symbol;
                }
                
                if (!string.IsNullOrEmpty(symbol))
                {
                    Clipboard.SetText(symbol);
                    _logger.LogDebug($"已复制合约名: {symbol}");
                }
            }
        }
        
        private void HeaderBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is Border border)
            {
                var dataGridName = border.Tag as string;
                if (string.IsNullOrEmpty(dataGridName)) return;
                
                DataGrid? targetDataGrid = null;
                
                switch (dataGridName)
                {
                    case "dgRealtime":
                        targetDataGrid = dgRealtime;
                        break;
                    case "dgCached":
                        targetDataGrid = dgCached;
                        break;
                    case "dgRebound1":
                        targetDataGrid = dgRebound1;
                        break;
                    case "dgRebound2":
                        targetDataGrid = dgRebound2;
                        break;
                    case "dgRecycled":
                        targetDataGrid = dgRecycled;
                        break;
                }
                
                if (targetDataGrid != null && targetDataGrid.ItemsSource != null)
                {
                    var symbols = new List<string>();
                    
                    foreach (var item in targetDataGrid.ItemsSource)
                    {
                        if (item is LoserContract loser)
                        {
                            symbols.Add(loser.Symbol);
                        }
                        else if (item is CachedLoserContract cached)
                        {
                            symbols.Add(cached.Symbol);
                        }
                        else if (item is RecycledLoserContract recycled)
                        {
                            symbols.Add(recycled.Symbol);
                        }
                    }
                    
                    if (symbols.Count > 0)
                    {
                        var text = string.Join(",", symbols);
                        Clipboard.SetText(text);
                        _logger.LogDebug($"已复制{dataGridName}区域合约: {symbols.Count}个");
                    }
                }
            }
        }
    }
}

