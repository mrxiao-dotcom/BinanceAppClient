using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace BinanceApps.WPF
{
    public partial class GainerTrackingWindow : Window
    {
        private readonly ILogger<GainerTrackingWindow> _logger;
        private readonly GainerTrackingService _gainerService;
        private readonly string _instanceId;
        
        private DispatcherTimer? _scanTimer;
        private DispatcherTimer? _countdownTimer;
        private bool _isMonitoring = false;
        private bool _isScanning = false;
        private DateTime _nextScanTime;
        private readonly object _dataLock = new object();
        
        // 数据
        private GainerTrackingConfig _config = new();
        private List<GainerContract> _realtimeGainers = new();
        private Dictionary<string, CachedGainerContract> _cachedContracts = new();
        private Dictionary<string, RecycledGainerContract> _recycledContracts = new();
        
        // 静态计数器
        private static int _windowCounter = 0;
        private readonly int _windowNumber;
        
        public GainerTrackingWindow(
            ILogger<GainerTrackingWindow> logger,
            GainerTrackingService gainerService)
        {
            InitializeComponent();
            
            _logger = logger;
            _gainerService = gainerService;
            
            _instanceId = "default";
            _windowNumber = System.Threading.Interlocked.Increment(ref _windowCounter);
            Title = $"近期涨幅榜追踪 - 窗口{_windowNumber}";
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation($"涨幅榜追踪窗口已加载: {_instanceId}");
            
            await LoadDataAsync();
            
            // 启动倒计时定时器
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();
        }
        
        private bool _isClosingConfirmed = false;
        
        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingConfirmed)
                return;
            
            e.Cancel = true;
            
            try
            {
                StopMonitoring();
                _countdownTimer?.Stop();
                
                if (ReadConfigFromUI())
                {
                    _logger.LogInformation("已从UI读取最新配置参数");
                }
                
                await Dispatcher.InvokeAsync(() => txtStatus.Text = "正在保存数据...");
                
                await SaveDataSyncAsync();
                
                _logger.LogInformation($"🔒 涨幅榜追踪窗口关闭完成: {_instanceId}");
                _logger.LogInformation($"   缓存合约: {_cachedContracts.Count}个");
                _logger.LogInformation($"   回收合约: {_recycledContracts.Count}个");
                
                _isClosingConfirmed = true;
                await Dispatcher.InvokeAsync(() => this.Close());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存数据时发生错误");
                _isClosingConfirmed = true;
                await Dispatcher.InvokeAsync(() => this.Close());
            }
        }
        
        private async Task LoadDataAsync()
        {
            try
            {
                var data = await Task.Run(async () => await _gainerService.LoadDataAsync(_instanceId));
                
                if (data != null)
                {
                    _config = data.Config;
                    _cachedContracts = data.CachedContracts;
                    _recycledContracts = data.RecycledContracts;
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateConfigUI();
                        RefreshAllDataGrids();
                        txtStatus.Text = "已加载上次保存的配置";
                    });
                    
                    _logger.LogInformation($"成功加载数据: 缓存={_cachedContracts.Count}, 回收={_recycledContracts.Count}");
                }
                else
                {
                    _logger.LogInformation("没有找到历史数据，使用默认配置");
                    await Dispatcher.InvokeAsync(() => txtStatus.Text = "使用默认配置");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载数据失败");
                await Dispatcher.InvokeAsync(() => txtStatus.Text = "加载配置失败，使用默认值");
            }
        }
        
        private async Task SaveDataSyncAsync()
        {
            try
            {
                _logger.LogInformation($"📝 准备保存数据: InstanceId={_instanceId}");
                _logger.LogInformation($"   缓存区数量: {_cachedContracts.Count}");
                _logger.LogInformation($"   回收区数量: {_recycledContracts.Count}");
                
                GainerTrackingData data;
                lock (_dataLock)
                {
                    data = new GainerTrackingData
                    {
                        Config = _config,
                        CachedContracts = new Dictionary<string, CachedGainerContract>(_cachedContracts),
                        RecycledContracts = new Dictionary<string, RecycledGainerContract>(_recycledContracts)
                    };
                }
                
                _logger.LogInformation($"   数据快照已创建");
                
                await _gainerService.SaveDataAsync(_instanceId, data);
                
                _logger.LogInformation($"✅ 同步保存完成: 缓存={data.CachedContracts.Count}, 回收={data.RecycledContracts.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 同步保存数据失败");
                throw;
            }
        }
        
        private void UpdateConfigUI()
        {
            txtNDays.Text = _config.NDays.ToString();
            txtTopCount.Text = _config.TopCount.ToString();
            txtPullbackZone1.Text = _config.PullbackZone1Threshold.ToString();
            txtPullbackZone2.Text = _config.PullbackZone2Threshold.ToString();
            txtScanInterval.Text = _config.ScanIntervalSeconds.ToString();
            txtCacheExpiry.Text = _config.CacheExpiryHours.ToString();
        }
        
        private bool ReadConfigFromUI()
        {
            try
            {
                _config.NDays = int.Parse(txtNDays.Text);
                _config.TopCount = int.Parse(txtTopCount.Text);
                _config.PullbackZone1Threshold = decimal.Parse(txtPullbackZone1.Text);
                _config.PullbackZone2Threshold = decimal.Parse(txtPullbackZone2.Text);
                _config.ScanIntervalSeconds = int.Parse(txtScanInterval.Text);
                _config.CacheExpiryHours = int.Parse(txtCacheExpiry.Text);
                
                if (_config.NDays <= 0 || _config.TopCount <= 0 ||
                    _config.PullbackZone1Threshold <= 0 || _config.PullbackZone2Threshold <= 0 ||
                    _config.ScanIntervalSeconds <= 0 || _config.CacheExpiryHours <= 0)
                {
                    MessageBox.Show("所有参数必须大于0", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"参数格式错误: {ex.Message}", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        
        private async void BtnToggleMonitoring_Click(object sender, RoutedEventArgs e)
        {
            if (_isMonitoring)
            {
                StopMonitoring();
            }
            else
            {
                if (!ReadConfigFromUI())
                    return;
                
                await StartMonitoringAsync();
            }
        }
        
        private async Task StartMonitoringAsync()
        {
            try
            {
                _isMonitoring = true;
                btnToggleMonitoring.Content = "停止监控";
                btnToggleMonitoring.Style = (Style)FindResource("StopButtonStyle");
                txtStatus.Text = "正在启动监控...";
                
                _logger.LogInformation($"监控已启动，使用配置: N天={_config.NDays}, 排行数={_config.TopCount}, 扫描间隔={_config.ScanIntervalSeconds}秒");
                
                _scanTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_config.ScanIntervalSeconds)
                };
                _scanTimer.Tick += async (s, e) => await ScanGainersAsync();
                _scanTimer.Start();
                
                _ = Task.Run(async () => await ScanGainersAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动监控失败");
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"启动监控失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                StopMonitoring();
            }
        }
        
        private void StopMonitoring()
        {
            _isMonitoring = false;
            _scanTimer?.Stop();
            _scanTimer = null;
            
            btnToggleMonitoring.Content = "启动监控";
            btnToggleMonitoring.Style = (Style)FindResource("ButtonStyle");
            txtStatus.Text = "已停止";
            txtNextScan.Text = "";
            
            _logger.LogInformation("监控已停止");
        }
        
        private async Task ScanGainersAsync()
        {
            if (_isScanning)
            {
                _logger.LogDebug("扫描正在进行中，跳过本次扫描");
                return;
            }
            
            _isScanning = true;
            
            try
            {
                await Dispatcher.InvokeAsync(() => txtStatus.Text = "正在扫描...");
                
                var gainers = await _gainerService.ScanTopGainersAsync(_config);
                
                lock (_dataLock)
                {
                    _realtimeGainers = gainers;
                }
                
                await _gainerService.UpdateCachedContractsAsync(_realtimeGainers, _cachedContracts, _config);
                
                await Task.Run(() =>
                {
                    lock (_dataLock)
                    {
                        _gainerService.CleanExpiredCache(_cachedContracts, _recycledContracts);
                        _gainerService.CleanRecycledContracts(_recycledContracts);
                    }
                });
                
                await Dispatcher.InvokeAsync(() =>
                {
                    lock (_dataLock)
                    {
                        RefreshAllDataGrids();
                    }
                    
                    _nextScanTime = DateTime.Now.AddSeconds(_config.ScanIntervalSeconds);
                    txtStatus.Text = $"监控中 - 涨幅榜:{_realtimeGainers.Count} | 缓存:{_cachedContracts.Count}";
                });
                
                try
                {
                    await SaveDataSyncAsync();
                    _logger.LogDebug($"✅ 扫描后数据已保存");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "扫描后保存数据失败，将在下次扫描时重试");
                }
                
                _logger.LogDebug($"扫描完成: 涨幅榜={_realtimeGainers.Count}, 缓存={_cachedContracts.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描涨幅榜失败");
                await Dispatcher.InvokeAsync(() => txtStatus.Text = $"扫描失败: {ex.Message}");
            }
            finally
            {
                _isScanning = false;
            }
        }
        
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (_isMonitoring && _nextScanTime > DateTime.Now)
            {
                var remaining = (_nextScanTime - DateTime.Now).TotalSeconds;
                txtNextScan.Text = $"下次扫描: {remaining:F0}秒";
            }
            else
            {
                txtNextScan.Text = "";
            }
            
            bool shouldRefresh = false;
            lock (_dataLock)
            {
                shouldRefresh = _cachedContracts.Count > 0 || _recycledContracts.Count > 0;
            }
            
            if (shouldRefresh)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    lock (_dataLock)
                    {
                        dgCached.Items.Refresh();
                        dgPullback1.Items.Refresh();
                        dgPullback2.Items.Refresh();
                    }
                });
            }
        }
        
        private void RefreshAllDataGrids()
        {
            // 1. 实时涨幅榜
            dgRealtime.ItemsSource = _realtimeGainers.OrderBy(g => g.Rank).ToList();
            txtRealtimeCount.Text = $"({_realtimeGainers.Count}个)";
            
            // 2. 缓存区
            dgCached.ItemsSource = _cachedContracts.Values
                .OrderByDescending(c => c.EntryTime)
                .ToList();
            txtCachedCount.Text = $"({_cachedContracts.Count}个)";
            
            // 3. 回撤一区
            var pullback1 = _cachedContracts.Values
                .Where(c => c.CurrentPullbackPercent >= _config.PullbackZone1Threshold)
                .OrderByDescending(c => c.CurrentPullbackPercent)
                .ToList();
            dgPullback1.ItemsSource = pullback1;
            txtPullback1Count.Text = $"({pullback1.Count}个)";
            
            // 4. 回撤二区
            var pullback2 = _cachedContracts.Values
                .Where(c => c.CurrentPullbackPercent >= _config.PullbackZone2Threshold)
                .OrderByDescending(c => c.CurrentPullbackPercent)
                .ToList();
            dgPullback2.ItemsSource = pullback2;
            txtPullback2Count.Text = $"({pullback2.Count}个)";
            
            // 5. 回收区
            dgRecycled.ItemsSource = _recycledContracts.Values
                .OrderByDescending(r => r.RecycleTime)
                .ToList();
            txtRecycledCount.Text = $"({_recycledContracts.Count}个)";
        }
        
        #region 双击复制功能
        
        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.DataGrid dataGrid && dataGrid.SelectedItem != null)
                {
                    var symbolProperty = dataGrid.SelectedItem.GetType().GetProperty("Symbol");
                    if (symbolProperty != null)
                    {
                        var symbol = symbolProperty.GetValue(dataGrid.SelectedItem)?.ToString();
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            Clipboard.SetText(symbol);
                            _logger.LogInformation($"✅ 已复制合约到剪贴板: {symbol}");
                            
                            var originalStatus = txtStatus.Text;
                            txtStatus.Text = $"已复制: {symbol}";
                            var statusTimer = new DispatcherTimer
                            {
                                Interval = TimeSpan.FromSeconds(2)
                            };
                            statusTimer.Tick += (s, args) =>
                            {
                                txtStatus.Text = originalStatus;
                                statusTimer.Stop();
                            };
                            statusTimer.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "复制合约名称时出错");
            }
        }
        
        private void HeaderBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ClickCount != 2)
                    return;
                
                if (sender is System.Windows.Controls.Border border && border.Tag != null)
                {
                    var dataGridName = border.Tag.ToString();
                    System.Windows.Controls.DataGrid? targetDataGrid = null;
                    
                    switch (dataGridName)
                    {
                        case "dgRealtime":
                            targetDataGrid = dgRealtime;
                            break;
                        case "dgCached":
                            targetDataGrid = dgCached;
                            break;
                        case "dgPullback1":
                            targetDataGrid = dgPullback1;
                            break;
                        case "dgPullback2":
                            targetDataGrid = dgPullback2;
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
                            var symbolProperty = item.GetType().GetProperty("Symbol");
                            if (symbolProperty != null)
                            {
                                var symbol = symbolProperty.GetValue(item)?.ToString();
                                if (!string.IsNullOrEmpty(symbol))
                                {
                                    symbols.Add(symbol);
                                }
                            }
                        }
                        
                        if (symbols.Count > 0)
                        {
                            var result = string.Join(",", symbols);
                            Clipboard.SetText(result);
                            _logger.LogInformation($"✅ 已复制 {symbols.Count} 个合约到剪贴板: {result}");
                            
                            var originalStatus = txtStatus.Text;
                            txtStatus.Text = $"已复制 {symbols.Count} 个合约";
                            var statusTimer = new DispatcherTimer
                            {
                                Interval = TimeSpan.FromSeconds(2)
                            };
                            statusTimer.Tick += (s, args) =>
                            {
                                txtStatus.Text = originalStatus;
                                statusTimer.Stop();
                            };
                            statusTimer.Start();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "复制区域合约列表时出错");
            }
        }
        
        #endregion
    }
}

