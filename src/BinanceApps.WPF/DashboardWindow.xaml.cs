using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace BinanceApps.WPF
{
    public partial class DashboardWindow : Window
    {
        private readonly ILogger<DashboardWindow> _logger;
        private readonly DashboardService _dashboardService;
        private DispatcherTimer? _autoRefreshTimer;
        
        // 期货和趋势投资名人名言列表
        private readonly List<(string Quote, string Author)> _investmentQuotes = new()
        {
            ("趋势一旦形成，就会延续下去。顺势而为，永远不要逆势操作。", "杰西·利弗莫尔"),
            ("在期货市场中，赚大钱的秘诀在于：及时止损，让利润奔跑。", "威廉·欧奈尔"),
            ("市场永远是对的，错的只是我们自己。学会与市场共舞，而不是对抗市场。", "乔治·索罗斯"),
            ("成功的交易者不是预测未来，而是对市场的变化做出快速反应。", "保罗·都铎·琼斯"),
            ("在牛市中赚钱容易，但真正的智慧在于熊市中保住本金。", "沃伦·巴菲特"),
            ("最好的交易机会往往出现在别人恐慌的时候。贪婪时要恐惧，恐惧时要贪婪。", "沃伦·巴菲特"),
            ("趋势就像河流，我们无法改变它的方向，只能顺流而下。", "拉瑞·威廉斯"),
            ("优秀的交易者懂得耐心等待最佳时机，而不是每天都要交易。", "斯坦利·克罗"),
            ("在期货交易中，资金管理比预测行情更重要。控制风险才能长期生存。", "约翰·墨菲"),
            ("挑选你的对手，在更容易赚钱的地方下注。这里不是你唯一的市场，或者你可以等这个市场热了再下注。", "匿名")
        };
        
        public DashboardWindow(ILogger<DashboardWindow> logger, DashboardService dashboardService)
        {
            InitializeComponent();
            
            _logger = logger;
            _dashboardService = dashboardService;
            
            // 窗口加载后自动刷新
            Loaded += DashboardWindow_Loaded;
        }
        
        private async void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 随机显示一条投资名言
            ShowRandomQuote();
            
            await LoadDashboardDataAsync();
        }
        
        /// <summary>
        /// 显示随机投资名言
        /// </summary>
        private void ShowRandomQuote()
        {
            var random = new Random();
            var index = random.Next(_investmentQuotes.Count);
            var (quote, author) = _investmentQuotes[index];
            
            // 更新投资建议文本
            txtInvestmentQuote.Text = quote;
            txtQuoteAuthor.Text = $"—— {author}";
        }
        
        /// <summary>
        /// 加载仪表板数据
        /// </summary>
        private async Task LoadDashboardDataAsync()
        {
            try
            {
                // 显示加载遮罩
                ShowLoading(true, "正在加载仪表板数据...");
                
                _logger.LogInformation("开始加载仪表板数据");
                
                // 获取仪表板数据
                var summary = await Task.Run(async () => 
                    await _dashboardService.GetDashboardSummaryAsync(30, 20, 5m));
                
                // 更新UI
                await Dispatcher.InvokeAsync(() => UpdateUI(summary));
                
                _logger.LogInformation("仪表板数据加载完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载仪表板数据时发生错误");
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }
        
        /// <summary>
        /// 更新UI显示
        /// </summary>
        private void UpdateUI(DashboardSummary summary)
        {
            // 更新时间
            txtUpdateTime.Text = $"更新时间: {summary.UpdateTime:HH:mm:ss}";
            
            // 1. 更新市场趋势综合分析区
            UpdateTrendAnalysisUI(summary.TrendAnalysis);
            
            // 2. 更新高低价位置分布
            UpdatePositionDistributionUI(summary.PositionStats, summary.UpdateTime);
            
            // 3. 更新24H市场动态
            UpdateMarketDynamicsUI(summary.MarketStats, summary.UpdateTime);
            
            // 4. 更新均线距离分布
            UpdateMaDistributionUI(summary.MaStats, summary.UpdateTime);
            
            // 5. 更新量比排行
            UpdateVolumeRatioUI(summary.VolumeRatioTop20, summary.UpdateTime);
            
            // 6. 更新30天从最低价涨幅TOP20
            Update30DayGainsUI(summary.Top20GainsFrom30DayLow, summary.UpdateTime);
            
            // 7. 更新30天从最高价跌幅TOP20
            Update30DayFallsUI(summary.Top20FallsFrom30DayHigh, summary.UpdateTime);
        }
        
        /// <summary>
        /// 更新市场趋势综合分析区
        /// </summary>
        private void UpdateTrendAnalysisUI(MarketTrendAnalysis analysis)
        {
            // 更新趋势标题
            txtTrendTitle.Text = analysis.TrendDescription;
            
            // 根据趋势设置颜色
            txtTrendTitle.Foreground = analysis.OverallTrend switch
            {
                MarketTrend.StrongBullish => new SolidColorBrush(Color.FromRgb(0, 160, 0)),
                MarketTrend.Bullish => new SolidColorBrush(Color.FromRgb(0, 180, 0)),
                MarketTrend.Sideways => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                MarketTrend.Bearish => new SolidColorBrush(Color.FromRgb(220, 50, 50)),
                MarketTrend.StrongBearish => new SolidColorBrush(Color.FromRgb(180, 0, 0)),
                _ => new SolidColorBrush(Colors.Gray)
            };
            
            // 更新信号分析
            panelSignals.Children.Clear();
            AddSignalItem(analysis.MaSignal);
            AddSignalItem(analysis.PositionSignal);
            AddSignalItem(analysis.ChangeSignal);
            AddSignalItem(analysis.VolatilitySignal);
            
            // 更新综合判断
            txtOverallJudgment.Text = $"【综合判断】{analysis.TrendDescription} ({analysis.BullishSignalCount}/4维度牛市信号) {analysis.TrendIcon}";
            
            // 更新操作建议
            panelSuggestions.Children.Clear();
            foreach (var suggestion in analysis.Suggestions)
            {
                var suggestionText = new TextBlock
                {
                    Text = $"• {suggestion}",
                    FontSize = 13,
                    Margin = new Thickness(0, 3, 0, 3),
                    TextWrapping = TextWrapping.Wrap
                };
                panelSuggestions.Children.Add(suggestionText);
            }
        }
        
        /// <summary>
        /// 添加信号项
        /// </summary>
        private void AddSignalItem(SignalDetail signal)
        {
            var signalText = new TextBlock
            {
                FontSize = 13,
                Margin = new Thickness(0, 3, 0, 3)
            };
            
            signalText.Inlines.Add(new System.Windows.Documents.Run($"• {signal.Name}: "));
            signalText.Inlines.Add(new System.Windows.Documents.Run(signal.SignalIcon)
            {
                FontWeight = FontWeights.Bold
            });
            signalText.Inlines.Add(new System.Windows.Documents.Run($" {signal.SignalText} ")
            {
                FontWeight = FontWeights.Bold,
                Foreground = signal.Signal switch
                {
                    MarketSignal.Bullish => new SolidColorBrush(Color.FromRgb(0, 160, 0)),
                    MarketSignal.Bearish => new SolidColorBrush(Color.FromRgb(220, 50, 50)),
                    MarketSignal.Neutral => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
                    _ => new SolidColorBrush(Colors.Gray)
                }
            });
            signalText.Inlines.Add(new System.Windows.Documents.Run($"({signal.RawData})")
            {
                Foreground = new SolidColorBrush(Colors.Gray)
            });
            
            panelSignals.Children.Add(signalText);
        }
        
        /// <summary>
        /// 更新高低价位置分布
        /// </summary>
        private void UpdatePositionDistributionUI(PositionDistribution position, DateTime updateTime)
        {
            var total = position.TotalCount;
            
            txtHighCount.Text = total > 0 
                ? $"{position.HighCount} ({(decimal)position.HighCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtMidHighCount.Text = total > 0 
                ? $"{position.MidHighCount} ({(decimal)position.MidHighCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtMidLowCount.Text = total > 0 
                ? $"{position.MidLowCount} ({(decimal)position.MidLowCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtLowCount.Text = total > 0 
                ? $"{position.LowCount} ({(decimal)position.LowCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtPositionUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
        }
        
        /// <summary>
        /// 更新24H市场动态
        /// </summary>
        private void UpdateMarketDynamicsUI(MarketDynamics market, DateTime updateTime)
        {
            txtTotalVolume.Text = market.TotalVolumeDisplay;
            txtVolumePosition.Text = $"📈 位置: {market.VolumePosition}";
            
            // 24H涨幅TOP5
            panelTopGainers.Children.Clear();
            if (market.TopGainers.Count > 0)
            {
                foreach (var item in market.TopGainers)
                {
                    var gainerText = new TextBlock
                    {
                        Text = $"📈 {item.Symbol}: +{item.ChangePercent:F2}%",
                        FontSize = 12,
                        Margin = new Thickness(0, 2, 0, 2),
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 160, 0)),
                        FontWeight = FontWeights.SemiBold,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = item.Symbol  // 保存Symbol用于复制
                    };
                    
                    // 添加鼠标悬停效果
                    gainerText.MouseEnter += (s, e) => 
                    {
                        gainerText.TextDecorations = TextDecorations.Underline;
                    };
                    gainerText.MouseLeave += (s, e) => 
                    {
                        gainerText.TextDecorations = null;
                    };
                    
                    // 添加双击复制功能
                    gainerText.MouseLeftButtonDown += ContractText_MouseLeftButtonDown;
                    
                    panelTopGainers.Children.Add(gainerText);
                }
            }
            else
            {
                var noDataText = new TextBlock
                {
                    Text = "暂无数据",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic
                };
                panelTopGainers.Children.Add(noDataText);
            }
            
            // 24H跌幅TOP5
            panelTopLosers.Children.Clear();
            if (market.TopLosers.Count > 0)
            {
                foreach (var item in market.TopLosers)
                {
                    var loserText = new TextBlock
                    {
                        Text = $"📉 {item.Symbol}: {item.ChangePercent:F2}%",
                        FontSize = 12,
                        Margin = new Thickness(0, 2, 0, 2),
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 50, 50)),
                        FontWeight = FontWeights.SemiBold,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Tag = item.Symbol  // 保存Symbol用于复制
                    };
                    
                    // 添加鼠标悬停效果
                    loserText.MouseEnter += (s, e) => 
                    {
                        loserText.TextDecorations = TextDecorations.Underline;
                    };
                    loserText.MouseLeave += (s, e) => 
                    {
                        loserText.TextDecorations = null;
                    };
                    
                    // 添加双击复制功能
                    loserText.MouseLeftButtonDown += ContractText_MouseLeftButtonDown;
                    
                    panelTopLosers.Children.Add(loserText);
                }
            }
            else
            {
                var noDataText = new TextBlock
                {
                    Text = "暂无数据",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic
                };
                panelTopLosers.Children.Add(noDataText);
            }
            
            // 涨跌分布
            txtRiseFallDistribution.Text = $"上涨: {market.RisingCount} | 下跌: {market.FallingCount} | 比例: {market.RisingRatio:F1}%";
            
            txtMarketUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
        }
        
        /// <summary>
        /// 更新均线距离分布
        /// </summary>
        private void UpdateMaDistributionUI(MaDistanceDistribution ma, DateTime updateTime)
        {
            var total = ma.TotalCount;
            
            txtAboveFar.Text = total > 0 
                ? $"{ma.AboveFarCount} ({(decimal)ma.AboveFarCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtAboveNear.Text = total > 0 
                ? $"{ma.AboveNearCount} ({(decimal)ma.AboveNearCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtBelowNear.Text = total > 0 
                ? $"{ma.BelowNearCount} ({(decimal)ma.BelowNearCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtBelowFar.Text = total > 0 
                ? $"{ma.BelowFarCount} ({(decimal)ma.BelowFarCount / total * 100:F1}%)" 
                : "0 (0%)";
            
            txtMaUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
        }
        
        /// <summary>
        /// 显示/隐藏加载遮罩
        /// </summary>
        private void ShowLoading(bool show, string status = "")
        {
            loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!string.IsNullOrEmpty(status))
            {
                txtLoadingStatus.Text = status;
            }
        }
        
        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
        }
        
        /// <summary>
        /// 自动刷新开启
        /// </summary>
        private void ChkAutoRefresh_Checked(object sender, RoutedEventArgs e)
        {
            if (_autoRefreshTimer == null)
            {
                _autoRefreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(5) // 每5分钟刷新一次
                };
                _autoRefreshTimer.Tick += async (s, args) => await LoadDashboardDataAsync();
            }
            
            _autoRefreshTimer.Start();
            _logger.LogInformation("自动刷新已开启（每5分钟）");
        }
        
        /// <summary>
        /// 自动刷新关闭
        /// </summary>
        private void ChkAutoRefresh_Unchecked(object sender, RoutedEventArgs e)
        {
            _autoRefreshTimer?.Stop();
            _logger.LogInformation("自动刷新已关闭");
        }
        
        /// <summary>
        /// 更新量比排行UI
        /// </summary>
        private void UpdateVolumeRatioUI(List<VolumeRatioItem> volumeRatioTop20, DateTime updateTime)
        {
            panelVolumeRatio.Children.Clear();
            
            if (volumeRatioTop20 == null || volumeRatioTop20.Count == 0)
            {
                var noDataText = new TextBlock
                {
                    Text = "暂无数据（需要加载合约流通量信息）",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                panelVolumeRatio.Children.Add(noDataText);
                txtVolumeRatioUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
                return;
            }
            
            int rank = 1;
            foreach (var item in volumeRatioTop20)
            {
                var itemPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                
                // 排名
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"{rank}. ",
                    FontSize = 11,
                    Width = 25,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 合约名称（可双击复制）
                var symbolText = new TextBlock
                {
                    Text = item.Symbol,
                    FontSize = 11,
                    Width = 100,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = item.Symbol  // 保存Symbol用于复制
                };
                
                // 添加鼠标悬停效果
                symbolText.MouseEnter += (s, e) => 
                {
                    symbolText.TextDecorations = TextDecorations.Underline;
                    symbolText.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                };
                symbolText.MouseLeave += (s, e) => 
                {
                    symbolText.TextDecorations = null;
                    symbolText.Foreground = new SolidColorBrush(Colors.Black);
                };
                
                // 添加双击复制功能
                symbolText.MouseLeftButtonDown += ContractText_MouseLeftButtonDown;
                
                itemPanel.Children.Add(symbolText);
                
                // 量比
                itemPanel.Children.Add(new TextBlock
                {
                    Text = item.VolumeRatioDisplay,
                    FontSize = 11,
                    Width = 80,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                    FontWeight = FontWeights.Bold
                });
                
                // 成交额
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"成交: {item.QuoteVolumeDisplay}",
                    FontSize = 10,
                    Width = 100,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 流通市值
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"市值: {item.MarketCapDisplay}",
                    FontSize = 10,
                    Width = 100,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 涨跌幅
                var changeColor = item.PriceChangePercent >= 0
                    ? Color.FromRgb(0, 160, 0)
                    : Color.FromRgb(220, 50, 50);
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"{(item.PriceChangePercent >= 0 ? "+" : "")}{item.PriceChangePercent:F2}%",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(changeColor)
                });
                
                panelVolumeRatio.Children.Add(itemPanel);
                rank++;
            }
            
            txtVolumeRatioUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
        }
        
        protected override void OnClosed(EventArgs e)
        {
            _autoRefreshTimer?.Stop();
            base.OnClosed(e);
        }
        
        /// <summary>
        /// 合约文本双击事件 - 复制合约符号到剪贴板
        /// </summary>
        private void ContractText_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && sender is TextBlock textBlock)
            {
                try
                {
                    // 从Tag获取合约符号
                    var symbol = textBlock.Tag?.ToString();
                    
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        // 复制到剪贴板
                        System.Windows.Clipboard.SetText(symbol);
                        
                        // 视觉反馈：临时改变颜色
                        var originalForeground = textBlock.Foreground;
                        textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                        
                        // 1秒后恢复原色
                        var timer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1)
                        };
                        timer.Tick += (s, args) =>
                        {
                            textBlock.Foreground = originalForeground;
                            timer.Stop();
                        };
                        timer.Start();
                        
                        _logger.LogInformation($"已复制合约符号到剪贴板: {symbol}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "复制合约符号失败");
                }
            }
        }
        
        /// <summary>
        /// 更新30天从最低价涨幅TOP20 UI
        /// </summary>
        private void Update30DayGainsUI(List<PriceChangeFrom30DayLowItem> items, DateTime updateTime)
        {
            panel30DayGains.Children.Clear();
            
            if (items == null || items.Count == 0)
            {
                var noDataText = new TextBlock
                {
                    Text = "暂无数据",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                panel30DayGains.Children.Add(noDataText);
                txt30DayGainsUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
                return;
            }
            
            int rank = 1;
            foreach (var item in items)
            {
                var itemPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                
                // 排名
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"{rank}. ",
                    FontSize = 11,
                    Width = 25,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 合约名称（可双击复制）
                var symbolText = new TextBlock
                {
                    Text = item.Symbol,
                    FontSize = 11,
                    Width = 100,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = item.Symbol
                };
                
                symbolText.MouseEnter += (s, e) => 
                {
                    symbolText.TextDecorations = TextDecorations.Underline;
                    symbolText.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                };
                symbolText.MouseLeave += (s, e) => 
                {
                    symbolText.TextDecorations = null;
                    symbolText.Foreground = new SolidColorBrush(Colors.Black);
                };
                symbolText.MouseLeftButtonDown += ContractText_MouseLeftButtonDown;
                
                itemPanel.Children.Add(symbolText);
                
                // 涨幅
                itemPanel.Children.Add(new TextBlock
                {
                    Text = item.GainPercentDisplay,
                    FontSize = 11,
                    Width = 80,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 160, 0)),
                    FontWeight = FontWeights.Bold
                });
                
                // 30天最低价
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"30日低: {item.Low30Day:F4}",
                    FontSize = 10,
                    Width = 120,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 当前价格
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"现价: {item.CurrentPrice:F4}",
                    FontSize = 10,
                    Width = 90,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 跌幅（相对最高价）
                var fallColor = item.FallFromHighPercent < 0 
                    ? Color.FromRgb(220, 50, 50)   // 红色
                    : Color.FromRgb(100, 100, 100); // 灰色（如果是正数或0）
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"回撤: {item.FallFromHighPercentDisplay}",
                    FontSize = 10,
                    Width = 80,
                    Foreground = new SolidColorBrush(fallColor)
                });
                
                panel30DayGains.Children.Add(itemPanel);
                rank++;
            }
            
            txt30DayGainsUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
        }
        
        /// <summary>
        /// 更新30天从最高价跌幅TOP20 UI
        /// </summary>
        private void Update30DayFallsUI(List<PriceChangeFrom30DayHighItem> items, DateTime updateTime)
        {
            panel30DayFalls.Children.Clear();
            
            if (items == null || items.Count == 0)
            {
                var noDataText = new TextBlock
                {
                    Text = "暂无数据",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                panel30DayFalls.Children.Add(noDataText);
                txt30DayFallsUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
                return;
            }
            
            int rank = 1;
            foreach (var item in items)
            {
                var itemPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                
                // 排名
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"{rank}. ",
                    FontSize = 11,
                    Width = 25,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 合约名称（可双击复制）
                var symbolText = new TextBlock
                {
                    Text = item.Symbol,
                    FontSize = 11,
                    Width = 100,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = item.Symbol
                };
                
                symbolText.MouseEnter += (s, e) => 
                {
                    symbolText.TextDecorations = TextDecorations.Underline;
                    symbolText.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                };
                symbolText.MouseLeave += (s, e) => 
                {
                    symbolText.TextDecorations = null;
                    symbolText.Foreground = new SolidColorBrush(Colors.Black);
                };
                symbolText.MouseLeftButtonDown += ContractText_MouseLeftButtonDown;
                
                itemPanel.Children.Add(symbolText);
                
                // 跌幅
                itemPanel.Children.Add(new TextBlock
                {
                    Text = item.FallPercentDisplay,
                    FontSize = 11,
                    Width = 80,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 50, 50)),
                    FontWeight = FontWeights.Bold
                });
                
                // 30天最高价
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"30日高: {item.High30Day:F4}",
                    FontSize = 10,
                    Width = 120,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 当前价格
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"现价: {item.CurrentPrice:F4}",
                    FontSize = 10,
                    Width = 90,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                });
                
                // 涨幅（相对最低价）
                var gainColor = item.GainFromLowPercent > 0 
                    ? Color.FromRgb(0, 160, 0)     // 绿色
                    : Color.FromRgb(100, 100, 100); // 灰色（如果是负数或0）
                itemPanel.Children.Add(new TextBlock
                {
                    Text = $"反弹: {item.GainFromLowPercentDisplay}",
                    FontSize = 10,
                    Width = 80,
                    Foreground = new SolidColorBrush(gainColor)
                });
                
                panel30DayFalls.Children.Add(itemPanel);
                rank++;
            }
            
            txt30DayFallsUpdate.Text = $"🕒 更新: {updateTime:HH:mm:ss}";
        }
    }
} 