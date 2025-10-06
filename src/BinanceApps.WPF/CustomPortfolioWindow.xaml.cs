using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace BinanceApps.WPF
{
    public partial class CustomPortfolioWindow : Window
    {
        private readonly ILogger<CustomPortfolioWindow> _logger;
        private readonly CustomPortfolioService _portfolioService;
        private readonly PortfolioGroupService? _groupService;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly KlineDataStorageService _klineStorageService;
        private readonly ContractInfoService _contractInfoService;
        private Timer? _autoUpdateTimer;
        private List<PortfolioRuntimeData> _portfolioRuntimeDataList = new();
        private string? _selectedPortfolioId;
        private string _currentGroupFilter = "全部"; // 当前选中的分组
        
        // 30天数据缓存（Key: Symbol, Value: (HighPrice, LowPrice)）
        private readonly Dictionary<string, (decimal HighPrice, decimal LowPrice)> _cache30DayData = new();
        
        // 组合列表排序状态
        private string _portfolioSortColumn = ""; // 当前排序列：Name, Change24h, Change30d, Count, Volume
        private bool _portfolioSortAscending = true; // 排序方向：true=升序，false=降序
        
        // 明细列表排序状态
        private string _currentSortColumn = ""; // 当前排序列：Change, Price, Volume
        private bool _sortAscending = false; // 排序方向：true=升序，false=降序
        
        public CustomPortfolioWindow(
            ILogger<CustomPortfolioWindow> logger,
            CustomPortfolioService portfolioService,
            PortfolioGroupService? groupService,
            IBinanceSimulatedApiClient apiClient,
            KlineDataStorageService klineStorageService,
            ContractInfoService contractInfoService)
        {
            InitializeComponent();
            _logger = logger;
            _portfolioService = portfolioService;
            _groupService = groupService;
            _contractInfoService = contractInfoService;
            _apiClient = apiClient;
            _klineStorageService = klineStorageService;
        }
        
        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("自定义板块监控窗口加载");
                
                // 初始化分组服务
                if (_groupService != null)
                {
                    await _groupService.InitializeAsync();
                }
                
                // 初始化组合服务
                await _portfolioService.InitializeAsync();
                
                // 加载组合数据
                await LoadPortfoliosAsync();
                
                // 启动自动更新
                StartAutoUpdate();
                
                _logger.LogInformation("自定义板块监控窗口初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "窗口加载失败");
                MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 停止自动更新定时器
            _autoUpdateTimer?.Dispose();
            _logger.LogInformation("自定义板块监控窗口关闭");
        }
        
        /// <summary>
        /// 启动自动更新定时器
        /// </summary>
        private void StartAutoUpdate()
        {
            _autoUpdateTimer = new Timer(
                async _ => await Dispatcher.InvokeAsync(async () => await RefreshPortfolioDataAsync()),
                null,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5)
            );
            _logger.LogInformation("自动更新已启动（每5秒）");
        }
        
        /// <summary>
        /// 加载组合数据
        /// </summary>
        private async Task LoadPortfoliosAsync()
        {
            try
            {
                var portfolios = _portfolioService.GetAllPortfolios();
                _logger.LogInformation($"加载了 {portfolios.Count} 个组合");
                
                // 创建运行时数据
                _portfolioRuntimeDataList = portfolios.Select(p => new PortfolioRuntimeData
                {
                    Portfolio = p,
                    SymbolsData = new List<PortfolioSymbolData>()
                }).ToList();
                
                // 初始化30天数据缓存（仅在首次加载时）
                await Initialize30DayDataCacheAsync();
                
                // 刷新数据
                await RefreshPortfolioDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载组合失败");
                MessageBox.Show($"加载组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 初始化30天数据缓存（从K线数据加载，仅执行一次）
        /// </summary>
        private async Task Initialize30DayDataCacheAsync()
        {
            try
            {
                _logger.LogInformation("开始初始化30天数据缓存...");
                
                // 清空缓存
                _cache30DayData.Clear();
                
                // 获取所有组合中的合约列表
                var allSymbols = _portfolioRuntimeDataList
                    .SelectMany(r => r.Portfolio.Symbols)
                    .Select(s => s.Symbol)
                    .Distinct()
                    .ToList();
                
                _logger.LogInformation($"需要加载 {allSymbols.Count} 个合约的30天数据");
                
                // 批量加载K线数据并计算30天高低价
                foreach (var symbol in allSymbols)
                {
                    var (klines, success, error) = await _klineStorageService.LoadKlineDataAsync(symbol);
                    
                    if (success && klines != null && klines.Count > 0)
                    {
                        // 取最近30天的数据
                        var klineData30d = klines
                            .OrderByDescending(k => k.OpenTime)
                            .Take(30)
                            .ToList();
                        
                        if (klineData30d.Count > 0)
                        {
                            var highPrice = klineData30d.Max(k => k.HighPrice);
                            var lowPrice = klineData30d.Min(k => k.LowPrice);
                            
                            _cache30DayData[symbol] = (highPrice, lowPrice);
                        }
                    }
                }
                
                _logger.LogInformation($"30天数据缓存初始化完成，已缓存 {_cache30DayData.Count} 个合约");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初始化30天数据缓存失败");
            }
        }
        

        /// <summary>
        /// 刷新组合数据（获取最新行情）
        /// </summary>
        private async Task RefreshPortfolioDataAsync()
        {
            try
            {
                // 获取24H Ticker数据
                var tickers = await _apiClient.GetAllTicksAsync();
                if (tickers == null || tickers.Count == 0)
                {
                    _logger.LogWarning("无法获取Ticker数据");
                    return;
                }
                
                // 更新每个组合的数据
                foreach (var runtimeData in _portfolioRuntimeDataList)
                {
                    var portfolio = runtimeData.Portfolio;
                    var symbolsData = new List<PortfolioSymbolData>();
                    
                    foreach (var symbol in portfolio.Symbols)
                    {
                        var ticker = tickers.FirstOrDefault(t => t.Symbol == symbol.Symbol);
                        if (ticker != null)
                        {
                            // 从缓存读取30天高低价，用tick的当前价重新计算涨幅
                            decimal highPrice30d = 0;
                            decimal lowPrice30d = 0;
                            decimal priceChange30d = 0;
                            
                            if (_cache30DayData.TryGetValue(symbol.Symbol, out var cached))
                            {
                                highPrice30d = cached.HighPrice;
                                lowPrice30d = cached.LowPrice;
                                
                                // 用tick的当前价格（最新收盘价）计算30天涨幅
                                priceChange30d = lowPrice30d > 0 
                                    ? ((ticker.LastPrice - lowPrice30d) / lowPrice30d) * 100 
                                    : 0;
                            }
                            
                            // 获取合约信息（流通量、备注）
                            var contractInfo = _contractInfoService.GetContractInfo(symbol.Symbol);
                            decimal circulatingMarketCap = 0;
                            decimal volumeRatio = 0;
                            string contractRemark = string.Empty;
                            
                            if (contractInfo != null && contractInfo.CirculatingSupply > 0)
                            {
                                // 计算流通市值
                                circulatingMarketCap = contractInfo.CirculatingSupply * ticker.LastPrice;
                                
                                // 计算量比（24H成交额 / 流通市值）
                                if (circulatingMarketCap > 0)
                                {
                                    volumeRatio = ticker.QuoteVolume / circulatingMarketCap;
                                }
                                
                                // 获取合约备注（优先使用Remark，如果没有则使用Description）
                                contractRemark = !string.IsNullOrWhiteSpace(contractInfo.Remark) 
                                    ? contractInfo.Remark 
                                    : (contractInfo.Description ?? string.Empty);
                            }
                            
                            symbolsData.Add(new PortfolioSymbolData
                            {
                                Symbol = symbol.Symbol,
                                Remark = symbol.Remark,
                                PriceChangePercent = ticker.PriceChangePercent,
                                LastPrice = ticker.LastPrice,
                                QuoteVolume = ticker.QuoteVolume,
                                HighPrice30d = highPrice30d,
                                LowPrice30d = lowPrice30d,
                                PriceChangePercent30d = priceChange30d,
                                CirculatingMarketCap = circulatingMarketCap,
                                VolumeRatio = volumeRatio,
                                ContractRemark = contractRemark
                            });
                        }
                    }
                    
                    // 计算平均涨幅（24H）
                    runtimeData.SymbolsData = symbolsData;
                    runtimeData.AveragePriceChangePercent = symbolsData.Any() 
                        ? symbolsData.Average(s => s.PriceChangePercent) 
                        : 0;
                    
                    // 计算平均涨幅（30天）
                    runtimeData.AveragePriceChangePercent30d = symbolsData.Any() 
                        ? symbolsData.Average(s => s.PriceChangePercent30d) 
                        : 0;
                    
                    runtimeData.LastUpdateTime = DateTime.Now;
                }
                
                // 更新UI
                DisplayPortfoliosList();
                
                // 如果有选中的组合，刷新明细
                if (!string.IsNullOrEmpty(_selectedPortfolioId))
                {
                    var selectedData = _portfolioRuntimeDataList.FirstOrDefault(r => r.Portfolio.Id == _selectedPortfolioId);
                    if (selectedData != null)
                    {
                        DisplayPortfolioDetails(selectedData);
                    }
                }
                
                // 更新最后更新时间
                txtLastUpdate.Text = $"最后更新: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新数据失败");
                // 静默失败，不打扰用户
            }
        }
        
        /// <summary>
        /// 显示组合列表
        /// </summary>
        private void DisplayPortfoliosList()
        {
            // 1. 更新分组标签
            UpdateGroupTabs();
            
            // 2. 创建表头
            CreatePortfolioListHeader();
            
            // 3. 清空组合列表
            panelPortfolios.Children.Clear();
            
            if (_portfolioRuntimeDataList.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "暂无组合\n点击上方\"创建组合\"按钮开始",
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontSize = 14,
                    Margin = new Thickness(20, 50, 20, 20),
                    TextWrapping = TextWrapping.Wrap
                };
                panelPortfolios.Children.Add(emptyText);
                return;
            }
            
            // 4. 根据当前分组筛选组合
            var filteredData = _currentGroupFilter == "全部" 
                ? _portfolioRuntimeDataList 
                : _portfolioRuntimeDataList.Where(r => r.Portfolio.GroupName == _currentGroupFilter).ToList();
            
            if (filteredData.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = $"「{_currentGroupFilter}」分组暂无组合",
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontSize = 14,
                    Margin = new Thickness(20, 50, 20, 20),
                    TextWrapping = TextWrapping.Wrap
                };
                panelPortfolios.Children.Add(emptyText);
                return;
            }
            
            // 5. 应用排序
            var sortedData = ApplyPortfolioSorting(filteredData);
            
            // 6. 显示筛选并排序后的组合
            int index = 1;
            foreach (var runtimeData in sortedData)
            {
                var row = CreatePortfolioRow(runtimeData, index);
                panelPortfolios.Children.Add(row);
                index++;
            }
        }
        
        /// <summary>
        /// 创建组合列表表头
        /// </summary>
        private void CreatePortfolioListHeader()
        {
            // 从XAML中查找表头Grid
            var headerGrid = this.FindName("gridPortfolioHeader") as Grid;
            if (headerGrid == null)
            {
                _logger.LogError("无法找到表头Grid: gridPortfolioHeader");
                return;
            }
            
            // 清空表头Grid的列定义和子元素
            headerGrid.ColumnDefinitions.Clear();
            headerGrid.Children.Clear();
            
            // 定义列宽（与内容行完全一致）
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // 序号
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 名称（弹性宽度）
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 24H涨幅
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 30天涨幅
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 数量
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 成交额
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 操作
            
            // 序号列（不可排序）
            var numberHeader = new TextBlock
            {
                Text = "序号",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.DarkGray),
                TextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(numberHeader, 0);
            headerGrid.Children.Add(numberHeader);
            
            // 可排序列
            int col = 1;
            var headers = new[]
            {
                ("Name", "组合名称"),
                ("Change24h", "24H涨幅"),
                ("Change30d", "30天涨幅"),
                ("Count", "数量"),
                ("Volume", "成交额")
            };
            
            foreach (var (column, title) in headers)
            {
                var header = CreateSortablePortfolioHeader(column, title);
                Grid.SetColumn(header, col);
                headerGrid.Children.Add(header);
                col++;
            }
            
            // 操作列（不可排序）
            var actionHeader = new TextBlock
            {
                Text = "操作",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.DarkGray),
                TextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(actionHeader, 6);
            headerGrid.Children.Add(actionHeader);
        }
        
        /// <summary>
        /// 创建可排序的表头
        /// </summary>
        private TextBlock CreateSortablePortfolioHeader(string column, string title)
        {
            var isCurrentColumn = _portfolioSortColumn == column;
            var arrow = isCurrentColumn ? (_portfolioSortAscending ? " ↑" : " ↓") : "";
            
            // 名称列左对齐，其他列居中对齐
            var alignment = column == "Name" ? TextAlignment.Left : TextAlignment.Center;
            var margin = column == "Name" ? new Thickness(5, 0, 5, 0) : new Thickness(0);
            
            var textBlock = new TextBlock
            {
                Text = title + arrow,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = isCurrentColumn 
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 212)) 
                    : new SolidColorBrush(Colors.DarkGray),
                TextAlignment = alignment,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = margin
            };
            
            // 点击排序
            textBlock.MouseLeftButtonDown += (s, e) =>
            {
                if (_portfolioSortColumn == column)
                {
                    _portfolioSortAscending = !_portfolioSortAscending;
                }
                else
                {
                    _portfolioSortColumn = column;
                    _portfolioSortAscending = true;
                }
                DisplayPortfoliosList();
            };
            
            // 鼠标悬停效果
            textBlock.MouseEnter += (s, e) =>
            {
                if (!isCurrentColumn)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 200));
                }
            };
            
            textBlock.MouseLeave += (s, e) =>
            {
                if (!isCurrentColumn)
                {
                    textBlock.Foreground = new SolidColorBrush(Colors.DarkGray);
                }
            };
            
            return textBlock;
        }
        
        /// <summary>
        /// 应用组合列表排序
        /// </summary>
        private List<PortfolioRuntimeData> ApplyPortfolioSorting(List<PortfolioRuntimeData> data)
        {
            if (string.IsNullOrEmpty(_portfolioSortColumn))
            {
                return data;
            }
            
            IOrderedEnumerable<PortfolioRuntimeData> sorted = _portfolioSortColumn switch
            {
                "Name" => _portfolioSortAscending 
                    ? data.OrderBy(d => d.Portfolio.Name) 
                    : data.OrderByDescending(d => d.Portfolio.Name),
                "Change24h" => _portfolioSortAscending 
                    ? data.OrderBy(d => d.AveragePriceChangePercent) 
                    : data.OrderByDescending(d => d.AveragePriceChangePercent),
                "Change30d" => _portfolioSortAscending 
                    ? data.OrderBy(d => d.AveragePriceChangePercent30d) 
                    : data.OrderByDescending(d => d.AveragePriceChangePercent30d),
                "Count" => _portfolioSortAscending 
                    ? data.OrderBy(d => d.Portfolio.SymbolCount) 
                    : data.OrderByDescending(d => d.Portfolio.SymbolCount),
                "Volume" => _portfolioSortAscending 
                    ? data.OrderBy(d => d.SymbolsData.Sum(s => s.QuoteVolume)) 
                    : data.OrderByDescending(d => d.SymbolsData.Sum(s => s.QuoteVolume)),
                _ => data.OrderBy(d => d.Portfolio.Name)
            };
            
            return sorted.ToList();
        }
        
        /// <summary>
        /// 更新分组标签栏
        /// </summary>
        private void UpdateGroupTabs()
        {
            panelGroupTabs.Children.Clear();
            
            // 添加"全部"标签
            var allTab = CreateGroupTab("全部", _portfolioRuntimeDataList.Count);
            panelGroupTabs.Children.Add(allTab);
            
            // 如果没有分组服务，仅显示"全部"
            if (_groupService == null)
            {
                return;
            }
            
            // 从分组服务获取所有分组
            var groups = _groupService.GetAllGroups();
            
            // 添加各分组标签
            foreach (var group in groups)
            {
                var count = _portfolioRuntimeDataList.Count(r => r.Portfolio.GroupName == group.Name);
                var tab = CreateGroupTab(group.Name, count);
                panelGroupTabs.Children.Add(tab);
            }
        }
        
        /// <summary>
        /// 创建分组标签按钮
        /// </summary>
        private Border CreateGroupTab(string groupName, int count)
        {
            var isSelected = _currentGroupFilter == groupName;
            
            var border = new Border
            {
                Background = isSelected 
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 212)) 
                    : new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(3, 2, 3, 2),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            var textBlock = new TextBlock
            {
                Text = $"{groupName} ({count})",
                Foreground = isSelected 
                    ? new SolidColorBrush(Colors.White) 
                    : new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                FontSize = 12,
                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal
            };
            
            border.Child = textBlock;
            
            // 点击事件
            border.MouseLeftButtonDown += (s, e) =>
            {
                _currentGroupFilter = groupName;
                DisplayPortfoliosList();
            };
            
            // 鼠标悬停效果
            border.MouseEnter += (s, e) =>
            {
                if (!isSelected)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(230, 240, 255));
                }
            };
            
            border.MouseLeave += (s, e) =>
            {
                if (!isSelected)
                {
                    border.Background = new SolidColorBrush(Colors.White);
                }
            };
            
            return border;
        }
        
        /// <summary>
        /// 创建组合卡片UI（优化为单行布局）
        /// </summary>
        private Border CreatePortfolioCard(PortfolioRuntimeData runtimeData)
        {
            var portfolio = runtimeData.Portfolio;
            var avgChange = runtimeData.AveragePriceChangePercent;
            
            // 计算组合总成交额
            var totalVolume = runtimeData.SymbolsData.Sum(s => s.QuoteVolume);
            var volumeDisplay = totalVolume >= 1_000_000_000 ? $"${totalVolume / 1_000_000_000:F2}B"
                : totalVolume >= 1_000_000 ? $"${totalVolume / 1_000_000:F1}M"
                : $"${totalVolume / 1_000:F0}K";
            
            // 确定颜色和箭头
            var changeColor = avgChange > 0 ? Colors.Green : (avgChange < 0 ? Colors.Red : Colors.Gray);
            var changeArrow = avgChange > 0 ? "↑" : (avgChange < 0 ? "↓" : "→");
            
            // 主容器
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5, 3, 5, 3),
                Padding = new Thickness(8),
                Background = _selectedPortfolioId == portfolio.Id 
                    ? new SolidColorBrush(Color.FromRgb(230, 240, 255))
                    : new SolidColorBrush(Colors.White),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            // 点击选中，双击复制合约列表
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) // 双击
                {
                    try
                    {
                        if (portfolio.Symbols != null && portfolio.Symbols.Count > 0)
                        {
                            // 提取 PortfolioSymbol 对象的 Symbol 属性
                            var symbolsText = string.Join(",", portfolio.Symbols.Select(ps => ps.Symbol));
                            System.Windows.Clipboard.SetText(symbolsText);
                            
                            // 临时显示反馈
                            var originalBackground = border.Background;
                            border.Background = new SolidColorBrush(Color.FromRgb(144, 238, 144)); // 浅绿色
                            
                            var timer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(300)
                            };
                            timer.Tick += (ts, te) =>
                            {
                                border.Background = originalBackground;
                                timer.Stop();
                            };
                            timer.Start();
                            
                            _logger.LogInformation($"已复制组合 '{portfolio.Name}' 的合约列表到剪贴板: {symbolsText}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"复制合约列表失败: {ex.Message}");
                    }
                }
                else // 单击
                {
                    _selectedPortfolioId = portfolio.Id;
                    DisplayPortfoliosList(); // 刷新列表以更新选中状态
                    DisplayPortfolioDetails(runtimeData);
                }
            };
            
            // 使用Grid布局来实现单行显示
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 组合信息
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 按钮
            
            // 左侧：组合信息
            var infoStack = new StackPanel();
            
            // 第一行：名称
            var nameText = new TextBlock
            {
                Text = portfolio.Name,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 3)
            };
            infoStack.Children.Add(nameText);
            
            // 第二行：24H涨幅 | 30天涨幅 | 成分数 | 成交额
            var dataPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            // 24H涨幅
            var changeText = new TextBlock
            {
                Text = $"24H:{(avgChange >= 0 ? "+" : "")}{avgChange:F2}% {changeArrow}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(changeColor),
                Margin = new Thickness(0, 0, 8, 0)
            };
            dataPanel.Children.Add(changeText);
            
            // 30天涨幅
            var avgChange30d = runtimeData.AveragePriceChangePercent30d;
            var changeColor30d = avgChange30d > 0 ? Colors.Green : (avgChange30d < 0 ? Colors.Red : Colors.Gray);
            var changeArrow30d = avgChange30d > 0 ? "↑" : (avgChange30d < 0 ? "↓" : "→");
            
            var change30dText = new TextBlock
            {
                Text = $"30天:{(avgChange30d >= 0 ? "+" : "")}{avgChange30d:F2}% {changeArrow30d}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(changeColor30d),
                Margin = new Thickness(0, 0, 10, 0)
            };
            dataPanel.Children.Add(change30dText);
            
            var countText = new TextBlock
            {
                Text = $"{portfolio.SymbolCount}个",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            dataPanel.Children.Add(countText);
            
            var volumeText = new TextBlock
            {
                Text = volumeDisplay,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center
            };
            dataPanel.Children.Add(volumeText);
            
            infoStack.Children.Add(dataPanel);
            
            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);
            
            // 右侧：图标按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            
            // 修改按钮
            var btnEdit = new Button
            {
                Content = "修",
                Width = 36,
                Height = 28,
                Margin = new Thickness(0, 0, 4, 0),
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                ToolTip = "修改组合",
                Padding = new Thickness(0)
            };
            btnEdit.Click += async (s, e) =>
            {
                e.Handled = true; // 防止触发卡片的点击事件
                await EditPortfolio(portfolio);
            };
            buttonPanel.Children.Add(btnEdit);
            
            // 删除按钮
            var btnDelete = new Button
            {
                Content = "删",
                Width = 36,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                ToolTip = "删除组合",
                Padding = new Thickness(0)
            };
            btnDelete.Click += async (s, e) =>
            {
                e.Handled = true;
                await DeletePortfolio(portfolio);
            };
            buttonPanel.Children.Add(btnDelete);
            
            Grid.SetColumn(buttonPanel, 1);
            grid.Children.Add(buttonPanel);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 创建组合行（表格样式）
        /// </summary>
        private Border CreatePortfolioRow(PortfolioRuntimeData runtimeData, int index)
        {
            var portfolio = runtimeData.Portfolio;
            var avgChange = runtimeData.AveragePriceChangePercent;
            var avgChange30d = runtimeData.AveragePriceChangePercent30d;
            
            // 计算组合总成交额
            var totalVolume = runtimeData.SymbolsData.Sum(s => s.QuoteVolume);
            var volumeDisplay = totalVolume >= 1_000_000_000 ? $"${totalVolume / 1_000_000_000:F2}B"
                : totalVolume >= 1_000_000 ? $"${totalVolume / 1_000_000:F1}M"
                : $"${totalVolume / 1_000:F0}K";
            
            // 主容器
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0),
                Padding = new Thickness(8, 5, 8, 5),
                Background = _selectedPortfolioId == portfolio.Id 
                    ? new SolidColorBrush(Color.FromRgb(230, 240, 255))
                    : new SolidColorBrush(Colors.White),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            // 点击选中，双击复制合约列表
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) // 双击
                {
                    try
                    {
                        if (portfolio.Symbols != null && portfolio.Symbols.Count > 0)
                        {
                            var symbolsText = string.Join(",", portfolio.Symbols.Select(ps => ps.Symbol));
                            System.Windows.Clipboard.SetText(symbolsText);
                            
                            // 临时显示反馈
                            var originalBackground = border.Background;
                            border.Background = new SolidColorBrush(Color.FromRgb(144, 238, 144)); // 浅绿色
                            
                            var timer = new System.Windows.Threading.DispatcherTimer
                            {
                                Interval = TimeSpan.FromMilliseconds(300)
                            };
                            timer.Tick += (ts, te) =>
                            {
                                border.Background = originalBackground;
                                timer.Stop();
                            };
                            timer.Start();
                            
                            _logger.LogInformation($"已复制组合 '{portfolio.Name}' 的合约列表到剪贴板: {symbolsText}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"复制合约列表失败: {ex.Message}");
                    }
                }
                else // 单击
                {
                    _selectedPortfolioId = portfolio.Id;
                    DisplayPortfoliosList(); // 刷新列表以更新选中状态
                    DisplayPortfolioDetails(runtimeData);
                }
            };
            
            // 使用Grid布局，列宽与表头一致
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch // 确保Grid填充整个容器宽度
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) }); // 序号
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 名称（弹性宽度）
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 24H涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 30天涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 数量
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 成交额
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // 操作
            
            // 序号
            var indexText = new TextBlock
            {
                Text = index.ToString(),
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Gray)
            };
            Grid.SetColumn(indexText, 0);
            grid.Children.Add(indexText);
            
            // 名称
            var nameText = new TextBlock
            {
                Text = portfolio.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(5, 0, 5, 0)
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);
            
            // 24H涨幅
            var change24hColor = avgChange > 0 ? Colors.Green : (avgChange < 0 ? Colors.Red : Colors.Gray);
            var change24hArrow = avgChange > 0 ? "↑" : (avgChange < 0 ? "↓" : "→");
            var change24hText = new TextBlock
            {
                Text = $"{(avgChange >= 0 ? "+" : "")}{avgChange:F2}% {change24hArrow}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(change24hColor),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(change24hText, 2);
            grid.Children.Add(change24hText);
            
            // 30天涨幅
            var change30dColor = avgChange30d > 0 ? Colors.Green : (avgChange30d < 0 ? Colors.Red : Colors.Gray);
            var change30dArrow = avgChange30d > 0 ? "↑" : (avgChange30d < 0 ? "↓" : "→");
            var change30dText = new TextBlock
            {
                Text = $"{(avgChange30d >= 0 ? "+" : "")}{avgChange30d:F2}% {change30dArrow}",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(change30dColor),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(change30dText, 3);
            grid.Children.Add(change30dText);
            
            // 数量
            var countText = new TextBlock
            {
                Text = $"{portfolio.SymbolCount}个",
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.DarkGray)
            };
            Grid.SetColumn(countText, 4);
            grid.Children.Add(countText);
            
            // 成交额
            var volumeText = new TextBlock
            {
                Text = volumeDisplay,
                FontSize = 12,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.DarkGray)
            };
            Grid.SetColumn(volumeText, 5);
            grid.Children.Add(volumeText);
            
            // 操作按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 修改按钮
            var btnEdit = new Button
            {
                Content = "改",
                Width = 32,
                Height = 22,
                FontSize = 11,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnEdit.Click += async (s, e) =>
            {
                e.Handled = true; // 阻止事件冒泡到父元素
                await EditPortfolio(portfolio);
            };
            buttonPanel.Children.Add(btnEdit);
            
            // 删除按钮
            var btnDelete = new Button
            {
                Content = "删",
                Width = 32,
                Height = 22,
                FontSize = 11,
                Padding = new Thickness(0),
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnDelete.Click += async (s, e) =>
            {
                e.Handled = true; // 阻止事件冒泡到父元素
                await DeletePortfolio(portfolio);
            };
            buttonPanel.Children.Add(btnDelete);
            
            Grid.SetColumn(buttonPanel, 6);
            grid.Children.Add(buttonPanel);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 显示组合明细
        /// </summary>
        private void DisplayPortfolioDetails(PortfolioRuntimeData runtimeData)
        {
            var portfolio = runtimeData.Portfolio;
            
            txtDetailTitle.Text = $"{portfolio.Name} - 明细";
            panelSymbolDetails.Children.Clear();
            
            if (runtimeData.SymbolsData.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "该组合暂无合约数据",
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontSize = 14,
                    Margin = new Thickness(20)
                };
                panelSymbolDetails.Children.Add(emptyText);
                return;
            }
            
            // 显示组合说明
            if (!string.IsNullOrEmpty(portfolio.Description))
            {
                var descBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(240, 248, 255)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                
                var descText = new TextBlock
                {
                    Text = $"📝 {portfolio.Description}",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
                };
                
                descBorder.Child = descText;
                panelSymbolDetails.Children.Add(descBorder);
            }
            
            // 添加表头
            panelSymbolDetails.Children.Add(CreateTableHeader());
            
            // 应用排序并显示合约列表
            var sortedData = ApplySorting(runtimeData.SymbolsData);
            int index = 1;
            foreach (var symbolData in sortedData)
            {
                var symbolCard = CreateSymbolDetailCard(symbolData, index);
                panelSymbolDetails.Children.Add(symbolCard);
                index++;
            }
        }
        
        /// <summary>
        /// 创建表头
        /// </summary>
        private Border CreateTableHeader()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 6, 10, 6)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });  // 序号
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // 合约名称
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 24H涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 当前价格
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 30天涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 30天最高
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 30天最低
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 24H成交额
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });  // 流通市值
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // 24H量比
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 合约备注
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 用户备注
            
            // 序号
            var numberHeader = new TextBlock
            {
                Text = "序号",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.DarkGray),
                TextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(numberHeader, 0);
            grid.Children.Add(numberHeader);
            
            // 合约名称
            var symbolHeader = new TextBlock
            {
                Text = "合约",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(symbolHeader, 1);
            grid.Children.Add(symbolHeader);
            
            // 24H涨幅（可排序）
            var changeHeader = CreateSortableHeader("24H涨幅", "Change", 2);
            grid.Children.Add(changeHeader);
            
            // 当前价格（可排序）
            var priceHeader = CreateSortableHeader("价格", "Price", 3);
            grid.Children.Add(priceHeader);
            
            // 30天涨幅（可排序）
            var change30dHeader = CreateSortableHeader("30天涨幅", "Change30d", 4);
            grid.Children.Add(change30dHeader);
            
            // 30天最高价
            var high30dHeader = new TextBlock
            {
                Text = "30天最高",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(high30dHeader, 5);
            grid.Children.Add(high30dHeader);
            
            // 30天最低价
            var low30dHeader = new TextBlock
            {
                Text = "30天最低",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(low30dHeader, 6);
            grid.Children.Add(low30dHeader);
            
            // 成交额（可排序）
            var volumeHeader = CreateSortableHeader("24H成交额", "Volume", 7);
            grid.Children.Add(volumeHeader);
            
            // 流通市值（可排序）
            var marketCapHeader = CreateSortableHeader("流通市值", "MarketCap", 8);
            grid.Children.Add(marketCapHeader);
            
            // 24H量比（可排序）
            var volumeRatioHeader = CreateSortableHeader("24H量比", "VolumeRatio", 9);
            grid.Children.Add(volumeRatioHeader);
            
            // 合约备注
            var contractRemarkHeader = new TextBlock
            {
                Text = "合约备注",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(contractRemarkHeader, 10);
            grid.Children.Add(contractRemarkHeader);
            
            // 用户备注
            var remarkHeader = new TextBlock
            {
                Text = "用户备注",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(remarkHeader, 11);
            grid.Children.Add(remarkHeader);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 创建可排序的表头
        /// </summary>
        private TextBlock CreateSortableHeader(string text, string columnName, int columnIndex)
        {
            var sortIndicator = "";
            if (_currentSortColumn == columnName)
            {
                sortIndicator = _sortAscending ? " ▲" : " ▼";
            }
            
            var header = new TextBlock
            {
                Text = text + sortIndicator,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "点击排序"
            };
            
            Grid.SetColumn(header, columnIndex);
            
            // 添加点击事件
            header.MouseDown += (s, e) =>
            {
                // 切换排序
                if (_currentSortColumn == columnName)
                {
                    _sortAscending = !_sortAscending;
                }
                else
                {
                    _currentSortColumn = columnName;
                    _sortAscending = false; // 默认降序
                }
                
                // 刷新显示
                if (!string.IsNullOrEmpty(_selectedPortfolioId))
                {
                    var selectedData = _portfolioRuntimeDataList.FirstOrDefault(r => r.Portfolio.Id == _selectedPortfolioId);
                    if (selectedData != null)
                    {
                        DisplayPortfolioDetails(selectedData);
                    }
                }
            };
            
            return header;
        }
        
        /// <summary>
        /// 应用排序
        /// </summary>
        private List<PortfolioSymbolData> ApplySorting(List<PortfolioSymbolData> data)
        {
            if (string.IsNullOrEmpty(_currentSortColumn))
            {
                // 默认按涨幅降序
                return data.OrderByDescending(s => s.PriceChangePercent).ToList();
            }
            
            IEnumerable<PortfolioSymbolData> sorted = data;
            
            switch (_currentSortColumn)
            {
                case "Change":
                    sorted = _sortAscending 
                        ? data.OrderBy(s => s.PriceChangePercent)
                        : data.OrderByDescending(s => s.PriceChangePercent);
                    break;
                case "Price":
                    sorted = _sortAscending 
                        ? data.OrderBy(s => s.LastPrice)
                        : data.OrderByDescending(s => s.LastPrice);
                    break;
                case "Change30d":
                    sorted = _sortAscending 
                        ? data.OrderBy(s => s.PriceChangePercent30d)
                        : data.OrderByDescending(s => s.PriceChangePercent30d);
                    break;
                case "Volume":
                    sorted = _sortAscending 
                        ? data.OrderBy(s => s.QuoteVolume)
                        : data.OrderByDescending(s => s.QuoteVolume);
                    break;
                case "MarketCap":
                    sorted = _sortAscending 
                        ? data.OrderBy(s => s.CirculatingMarketCap)
                        : data.OrderByDescending(s => s.CirculatingMarketCap);
                    break;
                case "VolumeRatio":
                    sorted = _sortAscending 
                        ? data.OrderBy(s => s.VolumeRatio)
                        : data.OrderByDescending(s => s.VolumeRatio);
                    break;
            }
            
            return sorted.ToList();
        }
        
        /// <summary>
        /// 创建合约明细卡片（优化为单行显示）
        /// </summary>
        private Border CreateSymbolDetailCard(PortfolioSymbolData symbolData, int index)
        {
            var changeColor = symbolData.PriceChangePercent > 0 ? Colors.Green 
                : (symbolData.PriceChangePercent < 0 ? Colors.Red : Colors.Gray);
            
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Colors.White)
            };
            
            // 使用Grid布局实现单行显示
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });  // 序号
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // 合约名称
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 24H涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 当前价格
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 30天涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 30天最高
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 30天最低
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 24H成交额
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) });  // 流通市值
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // 24H量比
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) }); // 合约备注
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 用户备注
            
            // 序号
            var indexText = new TextBlock
            {
                Text = index.ToString(),
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(indexText, 0);
            grid.Children.Add(indexText);
            
            // 合约名称（双击复制）
            var symbolText = new TextBlock
            {
                Text = symbolData.Symbol,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "双击复制合约名称"
            };
            
            // 添加双击复制功能
            symbolText.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(symbolData.Symbol);
                        _logger.LogInformation($"已复制合约名称: {symbolData.Symbol}");
                        
                        // 显示视觉反馈
                        var originalForeground = symbolText.Foreground;
                        symbolText.Foreground = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                        
                        // 0.5秒后恢复原色
                        var timer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(500)
                        };
                        timer.Tick += (ts, te) =>
                        {
                            symbolText.Foreground = originalForeground;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "复制到剪贴板失败");
                    }
                }
            };
            
            Grid.SetColumn(symbolText, 1);
            grid.Children.Add(symbolText);
            
            // 24H涨幅
            var changeText = new TextBlock
            {
                Text = symbolData.PriceChangeDisplay,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(changeColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(changeText, 2);
            grid.Children.Add(changeText);
            
            // 当前价格
            var priceText = new TextBlock
            {
                Text = symbolData.PriceDisplay,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(priceText, 3);
            grid.Children.Add(priceText);
            
            // 30天涨幅
            var change30dColor = symbolData.PriceChangePercent30d > 0 ? Colors.Green 
                : (symbolData.PriceChangePercent30d < 0 ? Colors.Red : Colors.Gray);
            
            var change30dText = new TextBlock
            {
                Text = symbolData.PriceChange30dDisplay,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(change30dColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(change30dText, 4);
            grid.Children.Add(change30dText);
            
            // 30天最高价
            var high30dText = new TextBlock
            {
                Text = symbolData.HighPrice30dDisplay,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(high30dText, 5);
            grid.Children.Add(high30dText);
            
            // 30天最低价
            var low30dText = new TextBlock
            {
                Text = symbolData.LowPrice30dDisplay,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(low30dText, 6);
            grid.Children.Add(low30dText);
            
            // 24H成交额
            var volumeText = new TextBlock
            {
                Text = symbolData.VolumeDisplay,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(volumeText, 7);
            grid.Children.Add(volumeText);
            
            // 流通市值
            var marketCapText = new TextBlock
            {
                Text = symbolData.CirculatingMarketCapDisplay,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(marketCapText, 8);
            grid.Children.Add(marketCapText);
            
            // 24H量比（判断阈值保持不变：50%=0.5, 20%=0.2）
            var volumeRatioColor = symbolData.VolumeRatio > 0.5m ? Colors.Red 
                : (symbolData.VolumeRatio > 0.2m ? Colors.Orange : Colors.Gray);
            
            var volumeRatioText = new TextBlock
            {
                Text = symbolData.VolumeRatioDisplay, // 现在显示为百分比格式
                FontSize = 10,
                FontWeight = symbolData.VolumeRatio > 0.5m ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(volumeRatioColor),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = symbolData.VolumeRatio > 0 
                    ? $"量比 {symbolData.VolumeRatio * 100:F2}% (24H成交额 ÷ 流通市值)" 
                    : null
            };
            Grid.SetColumn(volumeRatioText, 9);
            grid.Children.Add(volumeRatioText);
            
            // 合约备注
            var contractRemarkText = new TextBlock
            {
                Text = string.IsNullOrEmpty(symbolData.ContractRemark) ? "" : symbolData.ContractRemark,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 60
            };
            Grid.SetColumn(contractRemarkText, 10);
            grid.Children.Add(contractRemarkText);
            
            // 用户备注（支持多行显示）
            var remarkText = new TextBlock
            {
                Text = string.IsNullOrEmpty(symbolData.Remark) ? "" : symbolData.Remark,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap, // 支持多行
                MaxHeight = 60 // 限制最大高度
            };
            Grid.SetColumn(remarkText, 11);
            grid.Children.Add(remarkText);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 创建分组按钮点击
        /// </summary>
        private void BtnCreateGroup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_groupService == null)
                {
                    MessageBox.Show("分组服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var dialog = new GroupEditorDialog(_groupService)
                {
                    Owner = this
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // 刷新分组标签
                    DisplayPortfoliosList();
                    MessageBox.Show($"分组 '{dialog.GroupName}' 创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建分组失败");
                MessageBox.Show($"创建分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 创建组合按钮点击
        /// </summary>
        private async void BtnCreatePortfolio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new PortfolioEditorDialog(_portfolioService, _groupService, _apiClient)
                {
                    Owner = this
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // 重新加载数据
                    await LoadPortfoliosAsync();
                    MessageBox.Show("组合创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建组合失败");
                MessageBox.Show($"创建组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 刷新数据按钮点击
        /// </summary>
        private async void BtnRefreshData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnRefreshData.IsEnabled = false;
                btnRefreshData.Content = "刷新中...";
                
                await RefreshPortfolioDataAsync();
                
                MessageBox.Show("数据已刷新！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新失败");
                MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnRefreshData.IsEnabled = true;
                btnRefreshData.Content = "刷新数据";
            }
        }
        
        /// <summary>
        /// 编辑组合
        /// </summary>
        private async Task EditPortfolio(CustomPortfolio portfolio)
        {
            try
            {
                var dialog = new PortfolioEditorDialog(_portfolioService, _groupService, _apiClient, portfolio)
                {
                    Owner = this
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // 重新加载数据
                    await LoadPortfoliosAsync();
                    MessageBox.Show("组合修改成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "编辑组合失败");
                MessageBox.Show($"编辑组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 删除组合
        /// </summary>
        private async Task DeletePortfolio(CustomPortfolio portfolio)
        {
            try
            {
                var result = MessageBox.Show(
                    $"确定要删除组合 \"{portfolio.Name}\" 吗？\n此操作不可恢复！",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
                
                if (result == MessageBoxResult.Yes)
                {
                    await _portfolioService.DeletePortfolioAsync(portfolio.Id);
                    
                    // 如果删除的是当前选中的组合，清空选中
                    if (_selectedPortfolioId == portfolio.Id)
                    {
                        _selectedPortfolioId = null;
                        txtDetailTitle.Text = "请选择组合";
                        panelSymbolDetails.Children.Clear();
                    }
                    
                    // 重新加载数据
                    await LoadPortfoliosAsync();
                    
                    MessageBox.Show("组合已删除！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除组合失败");
                MessageBox.Show($"删除组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 导出组合按钮点击
        /// </summary>
        private async void BtnExportPortfolios_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取统计信息
                var (portfolioCount, totalSymbols, groups) = _portfolioService.GetStatistics();
                
                if (portfolioCount == 0)
                {
                    MessageBox.Show("没有组合可以导出", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 显示导出确认对话框
                var message = $"即将导出以下数据：\n\n" +
                             $"组合数量：{portfolioCount} 个\n" +
                             $"合约总数：{totalSymbols} 个\n" +
                             $"分组数量：{groups.Count} 个\n" +
                             (groups.Count > 0 ? $"分组列表：{string.Join(", ", groups)}\n" : "") +
                             $"\n是否继续？";
                
                var result = MessageBox.Show(message, "确认导出", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // 打开保存文件对话框
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出组合到JSON文件",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"custom_portfolios_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };
                
                if (saveDialog.ShowDialog() != true)
                {
                    return;
                }
                
                // 执行导出
                var success = await _portfolioService.ExportToFileAsync(saveDialog.FileName);
                
                if (success)
                {
                    var exportMessage = $"导出成功！\n\n" +
                                       $"文件路径：{saveDialog.FileName}\n" +
                                       $"组合数量：{portfolioCount} 个\n" +
                                       $"合约总数：{totalSymbols} 个";
                    
                    MessageBox.Show(exportMessage, "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger.LogInformation($"用户导出组合到: {saveDialog.FileName}");
                }
                else
                {
                    MessageBox.Show("导出失败，请查看日志了解详情", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导出组合失败");
                MessageBox.Show($"导出组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 导入组合按钮点击
        /// </summary>
        private async void BtnImportPortfolios_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 警告提示
                var warningResult = MessageBox.Show(
                    "警告：导入组合将会覆盖当前所有组合数据！\n\n" +
                    "系统会自动创建当前数据的备份。\n\n" +
                    "是否继续？",
                    "确认导入",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (warningResult != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // 打开文件选择对话框
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "选择要导入的JSON文件",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "json"
                };
                
                if (openDialog.ShowDialog() != true)
                {
                    return;
                }
                
                // 执行导入
                var success = await _portfolioService.ImportFromFileAsync(openDialog.FileName);
                
                if (success)
                {
                    // 重新加载数据
                    await LoadPortfoliosAsync();
                    
                    var (portfolioCount, totalSymbols, groups) = _portfolioService.GetStatistics();
                    
                    var importMessage = $"导入成功！\n\n" +
                                       $"组合数量：{portfolioCount} 个\n" +
                                       $"合约总数：{totalSymbols} 个\n" +
                                       $"分组数量：{groups.Count} 个\n\n" +
                                       $"原数据已自动备份。";
                    
                    MessageBox.Show(importMessage, "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    _logger.LogInformation($"用户导入组合从: {openDialog.FileName}");
                }
                else
                {
                    MessageBox.Show("导入失败，请确认文件格式正确", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "导入组合失败");
                MessageBox.Show($"导入组合失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 