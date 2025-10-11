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
    public partial class HotspotTrackingWindow : Window
    {
        private readonly ILogger<HotspotTrackingWindow> _logger;
        private readonly HotspotTrackingService _hotspotService;
        private readonly string _instanceId;
        
        private DispatcherTimer? _scanTimer;
        private DispatcherTimer? _countdownTimer;
        private bool _isMonitoring = false;
        private bool _isScanning = false; // 防止重复扫描
        private DateTime _nextScanTime;
        private readonly object _dataLock = new object(); // 数据访问锁
        
        // 数据
        private HotspotTrackingConfig _config = new();
        private List<HotspotContract> _volumeAnomalyContracts = new(); // 今日量比异动区（只要量比超阈值）
        private List<HotspotContract> _realtimeHotspots = new(); // 实时量比监控区（量比超阈值且超N天最高）
        private Dictionary<string, CachedHotspotContract> _cachedContracts = new();
        private Dictionary<string, RecycledHotspotContract> _recycledContracts = new();
        
        // 静态计数器，用于区分多个窗口
        private static int _windowCounter = 0;
        private readonly int _windowNumber;
        
        public HotspotTrackingWindow(
            ILogger<HotspotTrackingWindow> logger,
            HotspotTrackingService hotspotService)
        {
            InitializeComponent();
            
            _logger = logger;
            _hotspotService = hotspotService;
            
            // 使用固定的实例ID，确保数据能被持久化和加载
            // 多个窗口共享同一份数据
            _instanceId = "default";
            
            // 窗口编号用于标题显示
            _windowNumber = System.Threading.Interlocked.Increment(ref _windowCounter);
            Title = $"热点追踪 - 窗口{_windowNumber}";
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation($"热点追踪窗口已加载: {_instanceId}");
            
            // 加载保存的数据
            await LoadDataAsync();
            
            // 初始化倒计时定时器（每秒更新）
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
            // 如果已经确认关闭，则直接允许
            if (_isClosingConfirmed)
            {
                return;
            }
            
            // 第一次关闭请求：取消关闭，先保存数据
            e.Cancel = true;
            
            try
            {
                // 停止监控和定时器
                StopMonitoring();
                _countdownTimer?.Stop();
                
                // 读取当前UI配置（确保最新的参数被保存）
                try
                {
                    if (ReadConfigFromUI())
                    {
                        _logger.LogInformation("已从UI读取最新配置参数");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取UI配置失败，使用现有配置");
                }
                
                // 显示保存状态
                await Dispatcher.InvokeAsync(() => txtStatus.Text = "正在保存数据...");
                
                // 同步保存数据（确保保存完成）
                await SaveDataSyncAsync();
                
                _logger.LogInformation($"🔒 热点追踪窗口关闭完成: {_instanceId}");
                _logger.LogInformation($"   缓存合约: {_cachedContracts.Count}个");
                _logger.LogInformation($"   回收合约: {_recycledContracts.Count}个");
                
                // 确认关闭标志
                _isClosingConfirmed = true;
                
                // 真正关闭窗口
                await Dispatcher.InvokeAsync(() => this.Close());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 保存数据时发生错误");
                
                // 即使保存失败也允许关闭
                _isClosingConfirmed = true;
                await Dispatcher.InvokeAsync(() => this.Close());
            }
        }
        
        /// <summary>
        /// 加载数据（后台线程）
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                // 后台线程加载数据
                var data = await Task.Run(async () => await _hotspotService.LoadDataAsync(_instanceId));
                
                if (data != null)
                {
                    _config = data.Config;
                    _cachedContracts = data.CachedContracts;
                    _recycledContracts = data.RecycledContracts;
                    
                    // UI线程更新界面
                    await Dispatcher.InvokeAsync(() =>
                    {
                        UpdateConfigUI();
                        RefreshAllDataGrids();
                        txtStatus.Text = "已加载上次保存的配置";
                    });
                    
                    _logger.LogInformation($"成功加载数据: 缓存={_cachedContracts.Count}, 回收={_recycledContracts.Count}");
                    _logger.LogInformation($"加载的配置: 量比阈值={_config.VolumeRatioThreshold}%, N天={_config.HighPriceDays}, 间隔={_config.ScanIntervalSeconds}秒");
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
        
        /// <summary>
        /// 保存数据（后台线程，不等待）
        /// </summary>
        private async Task SaveDataAsync()
        {
            try
            {
                // 后台线程保存数据
                await Task.Run(async () =>
                {
                    var data = new HotspotTrackingData
                    {
                        Config = _config,
                        CachedContracts = _cachedContracts,
                        RecycledContracts = _recycledContracts
                    };
                    
                    await _hotspotService.SaveDataAsync(_instanceId, data);
                });
                
                _logger.LogDebug("数据已保存");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存数据失败");
            }
        }
        
        /// <summary>
        /// 同步保存数据（确保完成）
        /// </summary>
        private async Task SaveDataSyncAsync()
        {
            try
            {
                _logger.LogInformation($"📝 准备保存数据: InstanceId={_instanceId}");
                _logger.LogInformation($"   缓存区数量: {_cachedContracts.Count}");
                _logger.LogInformation($"   回收区数量: {_recycledContracts.Count}");
                
                // 加锁读取数据快照
                HotspotTrackingData data;
                lock (_dataLock)
                {
                    data = new HotspotTrackingData
                    {
                        Config = _config,
                        CachedContracts = new Dictionary<string, CachedHotspotContract>(_cachedContracts),
                        RecycledContracts = new Dictionary<string, RecycledHotspotContract>(_recycledContracts)
                    };
                }
                
                _logger.LogInformation($"   数据快照已创建");
                
                // 直接调用，不使用 Task.Run
                await _hotspotService.SaveDataAsync(_instanceId, data);
                
                _logger.LogInformation($"✅ 同步保存完成: 缓存={data.CachedContracts.Count}, 回收={data.RecycledContracts.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 同步保存数据失败");
                throw; // 重新抛出异常以便上层处理
            }
        }
        
        /// <summary>
        /// 更新配置UI
        /// </summary>
        private void UpdateConfigUI()
        {
            txtVolumeRatioThreshold.Text = _config.VolumeRatioThreshold.ToString();
            txtHighPriceDays.Text = _config.HighPriceDays.ToString();
            txtPullbackZone1.Text = _config.PullbackZone1Threshold.ToString();
            txtPullbackZone2.Text = _config.PullbackZone2Threshold.ToString();
            txtScanInterval.Text = _config.ScanIntervalSeconds.ToString();
            txtCacheExpiry.Text = _config.CacheExpiryHours.ToString();
            txtMinMarketCap.Text = _config.MinCirculatingMarketCap.ToString();
            txtMaxMarketCap.Text = _config.MaxCirculatingMarketCap.ToString();
        }
        
        /// <summary>
        /// 从UI读取配置
        /// </summary>
        private bool ReadConfigFromUI()
        {
            try
            {
                _config.VolumeRatioThreshold = decimal.Parse(txtVolumeRatioThreshold.Text);
                _config.HighPriceDays = int.Parse(txtHighPriceDays.Text);
                _config.PullbackZone1Threshold = decimal.Parse(txtPullbackZone1.Text);
                _config.PullbackZone2Threshold = decimal.Parse(txtPullbackZone2.Text);
                _config.ScanIntervalSeconds = int.Parse(txtScanInterval.Text);
                _config.CacheExpiryHours = int.Parse(txtCacheExpiry.Text);
                _config.MinCirculatingMarketCap = decimal.Parse(txtMinMarketCap.Text);
                _config.MaxCirculatingMarketCap = decimal.Parse(txtMaxMarketCap.Text);
                
                // 验证参数
                if (_config.VolumeRatioThreshold <= 0 || _config.HighPriceDays <= 0 ||
                    _config.PullbackZone1Threshold <= 0 || _config.PullbackZone2Threshold <= 0 ||
                    _config.ScanIntervalSeconds <= 0 || _config.CacheExpiryHours <= 0)
                {
                    MessageBox.Show("所有参数必须大于0", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                
                // 验证流通市值范围
                if (_config.MinCirculatingMarketCap < 0 || _config.MaxCirculatingMarketCap < 0)
                {
                    MessageBox.Show("流通市值必须大于等于0", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                
                if (_config.MinCirculatingMarketCap > _config.MaxCirculatingMarketCap)
                {
                    MessageBox.Show("最小流通市值不能大于最大流通市值", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                
                return true;
            }
            catch
            {
                MessageBox.Show("请输入有效的数字参数", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
        
        
        /// <summary>
        /// 启动/停止监控按钮
        /// </summary>
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
        
        /// <summary>
        /// 启动监控（不阻塞UI）
        /// </summary>
        private async Task StartMonitoringAsync()
        {
            try
            {
                // 注意：请确保K线数据是最新的
                // 如需下载K线数据，请从主窗口点击"下载K线"按钮
                
                
                _isMonitoring = true;
                btnToggleMonitoring.Content = "停止监控";
                btnToggleMonitoring.Style = (Style)FindResource("StopButtonStyle");
                txtStatus.Text = "正在启动监控...";
                
                // 保存当前配置（后台线程）
                _ = SaveDataAsync(); // 不等待保存完成
                
                _logger.LogInformation($"监控已启动，使用配置: 量比阈值={_config.VolumeRatioThreshold}%, 扫描间隔={_config.ScanIntervalSeconds}秒");
                
                // 启动定时器
                _scanTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_config.ScanIntervalSeconds)
                };
                _scanTimer.Tick += async (s, e) => await ScanHotspotsAsync();
                _scanTimer.Start();
                
                // 立即在后台执行一次扫描（不等待完成，避免阻塞UI）
                _ = Task.Run(async () => await ScanHotspotsAsync());
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
        
        /// <summary>
        /// 停止监控
        /// </summary>
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
        
        /// <summary>
        /// 扫描热点合约（后台线程，防止重复执行）
        /// </summary>
        private async Task ScanHotspotsAsync()
        {
            // 防止重复扫描
            if (_isScanning)
            {
                _logger.LogDebug("扫描正在进行中，跳过本次扫描");
                return;
            }
            
            _isScanning = true;
            
            try
            {
                // 在UI线程更新状态并读取最新配置
                bool configValid = false;
                await Dispatcher.InvokeAsync(() =>
                {
                    txtStatus.Text = "正在扫描...";
                    // 每次扫描前读取最新的UI配置，使参数修改实时生效
                    configValid = ReadConfigFromUI();
                });
                
                if (!configValid)
                {
                    _logger.LogWarning("配置参数无效，跳过本次扫描");
                    await Dispatcher.InvokeAsync(() => txtStatus.Text = "配置参数无效");
                    return;
                }
                
                // 1. 后台线程扫描热点合约和量比异动（使用最新配置）
                var (volumeAnomalies, hotspots) = await _hotspotService.ScanHotspotContractsWithAnomalyAsync(_config);
                
                // 2. 加锁更新共享数据
                lock (_dataLock)
                {
                    _volumeAnomalyContracts = volumeAnomalies;
                    _realtimeHotspots = hotspots;
                }
                
                // 3. 更新缓存区（异步操作）
                await _hotspotService.UpdateCachedContractsAsync(_realtimeHotspots, _cachedContracts, _config);
                
                // 4. 清理过期缓存（后台线程）
                await Task.Run(() =>
                {
                    lock (_dataLock)
                    {
                        _hotspotService.CleanExpiredCache(_cachedContracts, _recycledContracts);
                        _hotspotService.CleanRecycledContracts(_recycledContracts);
                    }
                });
                
                // 5. UI线程刷新界面
                await Dispatcher.InvokeAsync(() =>
                {
                    lock (_dataLock)
                    {
                        RefreshAllDataGrids();
                    }
                    
                    // 更新状态
                    _nextScanTime = DateTime.Now.AddSeconds(_config.ScanIntervalSeconds);
                    txtStatus.Text = $"监控中 - 量比异动:{_volumeAnomalyContracts.Count} | 实时热点:{_realtimeHotspots.Count} | 缓存:{_cachedContracts.Count}";
                });
                
                // 6. 后台线程保存数据（同步等待，确保数据不丢失）
                try
                {
                    await SaveDataSyncAsync();
                    _logger.LogDebug($"✅ 扫描后数据已保存");
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "扫描后保存数据失败，将在下次扫描时重试");
                }
                
                _logger.LogDebug($"扫描完成: 量比异动={_volumeAnomalyContracts.Count}, 实时热点={_realtimeHotspots.Count}, 缓存={_cachedContracts.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "扫描热点合约失败");
                await Dispatcher.InvokeAsync(() => txtStatus.Text = $"扫描失败: {ex.Message}");
            }
            finally
            {
                _isScanning = false;
            }
        }
        
        /// <summary>
        /// 倒计时定时器（线程安全）
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            // 更新下次扫描倒计时
            if (_isMonitoring && _nextScanTime > DateTime.Now)
            {
                var remaining = (_nextScanTime - DateTime.Now).TotalSeconds;
                txtNextScan.Text = $"下次扫描: {remaining:F0}秒";
            }
            else
            {
                txtNextScan.Text = "";
            }
            
            // 刷新缓存区和回调区（更新剩余时间，使用锁保护）
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
        
        /// <summary>
        /// 刷新所有数据表格
        /// </summary>
        private void RefreshAllDataGrids()
        {
            // 1. 今日量比异动区（只要量比超阈值）
            dgVolumeAnomaly.ItemsSource = _volumeAnomalyContracts.OrderByDescending(h => h.VolumeRatio).ToList();
            txtVolumeAnomalyCount.Text = $"({_volumeAnomalyContracts.Count}个)";
            
            // 2. 实时监控区（量比超阈值且超N天最高）
            dgRealtime.ItemsSource = _realtimeHotspots.OrderByDescending(h => h.VolumeRatio).ToList();
            txtRealtimeCount.Text = $"({_realtimeHotspots.Count}个)";
            
            // 3. 缓存区
            dgCached.ItemsSource = _cachedContracts.Values
                .OrderByDescending(c => c.EntryTime)
                .ToList();
            txtCachedCount.Text = $"({_cachedContracts.Count}个)";
            
            // 4. 回调一区
            var pullback1 = _cachedContracts.Values
                .Where(c => c.CurrentPullbackPercent >= _config.PullbackZone1Threshold)
                .OrderByDescending(c => c.CurrentPullbackPercent)
                .ToList();
            dgPullback1.ItemsSource = pullback1;
            txtPullback1Count.Text = $"({pullback1.Count}个)";
            
            // 5. 回调二区
            var pullback2 = _cachedContracts.Values
                .Where(c => c.CurrentPullbackPercent >= _config.PullbackZone2Threshold)
                .OrderByDescending(c => c.CurrentPullbackPercent)
                .ToList();
            dgPullback2.ItemsSource = pullback2;
            txtPullback2Count.Text = $"({pullback2.Count}个)";
            
            // 6. 回收区
            dgRecycled.ItemsSource = _recycledContracts.Values
                .OrderByDescending(r => r.RecycleTime)
                .ToList();
            txtRecycledCount.Text = $"({_recycledContracts.Count}个)";
        }
        
        #region 双击复制功能
        
        /// <summary>
        /// DataGrid双击事件 - 复制合约名称
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.DataGrid dataGrid && dataGrid.SelectedItem != null)
                {
                    // 获取Symbol属性
                    var symbolProperty = dataGrid.SelectedItem.GetType().GetProperty("Symbol");
                    if (symbolProperty != null)
                    {
                        var symbol = symbolProperty.GetValue(dataGrid.SelectedItem)?.ToString();
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            Clipboard.SetText(symbol);
                            _logger.LogInformation($"✅ 已复制合约到剪贴板: {symbol}");
                            
                            // 更新状态栏显示（短暂提示）
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
        
        /// <summary>
        /// 头部Border双击事件 - 复制整个区域的所有合约
        /// </summary>
        private void HeaderBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                // 检测双击
                if (e.ClickCount != 2)
                    return;
                
                if (sender is System.Windows.Controls.Border border && border.Tag != null)
                {
                    var dataGridName = border.Tag.ToString();
                    System.Windows.Controls.DataGrid? targetDataGrid = null;
                    
                    // 根据Tag找到对应的DataGrid
                    switch (dataGridName)
                    {
                        case "dgVolumeAnomaly":
                            targetDataGrid = dgVolumeAnomaly;
                            break;
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
                            
                            // 更新状态栏显示
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

