using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace BinanceApps.WPF
{
    public partial class MarketDistributionWindow : Window
    {
        private readonly ILogger<MarketDistributionWindow> _logger;
        private readonly MarketDistributionService _distributionService;
        private MarketDistributionAnalysisResult? _currentResult;

        public MarketDistributionWindow(
            ILogger<MarketDistributionWindow> logger,
            MarketDistributionService distributionService)
        {
            InitializeComponent();
            _logger = logger;
            _distributionService = distributionService;

            Loaded += MarketDistributionWindow_Loaded;
        }

        private async void MarketDistributionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                ShowLoading(true, "正在分析市场数据...");
                txtStatus.Text = "加载中...";

                _currentResult = await Task.Run(() => _distributionService.GetDistributionAsync(5));

                if (_currentResult != null && _currentResult.DailyDistributions.Count > 0)
                {
                    DisplayDataList();
                    DrawChart();
                    txtStatus.Text = $"✅ 数据加载完成 ({DateTime.Now:HH:mm:ss})";
                }
                else
                {
                    txtStatus.Text = "⚠️ 暂无数据";
                    MessageBox.Show("未能加载到有效数据，请稍后重试。", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载市场分布数据失败");
                txtStatus.Text = "❌ 加载失败";
                MessageBox.Show($"加载数据失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowLoading(false);
            }
        }

        /// <summary>
        /// 显示/隐藏加载状态
        /// </summary>
        private void ShowLoading(bool show, string status = "")
        {
            loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            txtLoadingStatus.Text = status;
        }

        /// <summary>
        /// 显示数据列表
        /// </summary>
        private void DisplayDataList()
        {
            panelDataList.Children.Clear();

            if (_currentResult == null || _currentResult.DailyDistributions.Count == 0)
                return;

            // 添加表头
            panelDataList.Children.Add(CreateListHeader());

            // 添加数据行
            foreach (var distribution in _currentResult.DailyDistributions)
            {
                panelDataList.Children.Add(CreateDataRow(distribution));
            }
        }

        /// <summary>
        /// 创建列表表头
        /// </summary>
        private Border CreateListHeader()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 2)
            };

            var grid = new Grid();
            
            // 定义列
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 日期
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) }); // 总数
            
            // 为每个档位添加列
            var ranges = DailyPriceChangeDistribution.GetAllRanges();
            foreach (var _ in ranges)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // 日期
            AddHeaderText(grid, "日期", 0);
            AddHeaderText(grid, "总数", 1);

            // 档位名称
            int colIndex = 2;
            foreach (var range in ranges)
            {
                var name = DailyPriceChangeDistribution.GetRangeName(range);
                AddHeaderText(grid, name, colIndex);
                colIndex++;
            }

            border.Child = grid;
            return border;
        }

        /// <summary>
        /// 添加表头文本
        /// </summary>
        private void AddHeaderText(Grid grid, string text, int column)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.Black),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }

        /// <summary>
        /// 创建数据行
        /// </summary>
        private Border CreateDataRow(DailyPriceChangeDistribution distribution)
        {
            var border = new Border
            {
                Background = distribution.IsToday 
                    ? new SolidColorBrush(Color.FromRgb(255, 252, 230)) 
                    : new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6, 10, 6)
            };

            var grid = new Grid();
            
            // 定义列（与表头一致）
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            
            var ranges = DailyPriceChangeDistribution.GetAllRanges();
            foreach (var _ in ranges)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // 日期
            var dateText = distribution.Date.ToString("yyyy-MM-dd");
            if (distribution.IsToday)
                dateText += " ⭐";
            AddCellText(grid, dateText, 0, distribution.IsToday);

            // 总数（使用热力图背景）
            AddCellTextWithHeatmap(grid, distribution.TotalSymbols, 1, distribution.IsToday);

            // 各档位数量（使用热力图背景）
            int colIndex = 2;
            foreach (var range in ranges)
            {
                var count = distribution.RangeCounts.ContainsKey(range) 
                    ? distribution.RangeCounts[range] 
                    : 0;
                AddCellTextWithHeatmap(grid, count, colIndex, distribution.IsToday);
                colIndex++;
            }

            border.Child = grid;
            return border;
        }

        /// <summary>
        /// 添加单元格文本
        /// </summary>
        private void AddCellText(Grid grid, string text, int column, bool isBold = false)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Colors.Black),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }
        
        /// <summary>
        /// 添加带热力图背景的单元格文本（用于数值）
        /// </summary>
        private void AddCellTextWithHeatmap(Grid grid, int value, int column, bool isBold = false)
        {
            // 创建带背景的Border
            var border = new Border
            {
                Background = new SolidColorBrush(GetValueHeatmapColor(value)),
                Padding = new Thickness(4, 2, 4, 2)
            };
            
            var textBlock = new TextBlock
            {
                Text = value.ToString(),
                FontSize = 13,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Colors.Black),
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            border.Child = textBlock;
            Grid.SetColumn(border, column);
            grid.Children.Add(border);
        }
        
        /// <summary>
        /// 获取数值热力图颜色（0-白色，600-大红）
        /// </summary>
        private Color GetValueHeatmapColor(int value)
        {
            if (value == 0)
                return Colors.White; // 0 无色
            
            // 计算比例（0-600映射到0-1）
            double ratio = Math.Min((double)value / 600.0, 1.0);
            
            // 从白色(255,255,255)渐变到大红色(220,20,20)
            byte r = (byte)(255 - (35 * ratio));   // 255 -> 220
            byte g = (byte)(255 - (235 * ratio));  // 255 -> 20
            byte b = (byte)(255 - (235 * ratio));  // 255 -> 20
            
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// 绘制折线图
        /// </summary>
        private void DrawChart()
        {
            chartCanvas.Children.Clear();

            if (_currentResult == null || _currentResult.DailyDistributions.Count == 0)
                return;

            // 等待Canvas渲染完成后获取实际尺寸
            Dispatcher.InvokeAsync(() =>
            {
                var width = chartCanvas.ActualWidth;
                var height = chartCanvas.ActualHeight;

                if (width <= 0 || height <= 0)
                {
                    // 如果尺寸无效，稍后重试
                    Task.Delay(100).ContinueWith(_ => Dispatcher.Invoke(DrawChart));
                    return;
                }

                DrawChartContent(width, height);
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 绘制图表内容
        /// </summary>
        private void DrawChartContent(double width, double height)
        {
            if (_currentResult == null || _currentResult.DailyDistributions.Count == 0)
                return;
            
            var ranges = DailyPriceChangeDistribution.GetAllRanges();
            var rangeCount = ranges.Count;

            // 定义边距
            var marginLeft = 60;
            var marginRight = 20;
            var marginTop = 30;
            var marginBottom = 60;

            var chartWidth = width - marginLeft - marginRight;
            var chartHeight = height - marginTop - marginBottom;

            // 计算最大值（用于Y轴缩放）
            int maxCount = 0;
            foreach (var dist in _currentResult.DailyDistributions)
            {
                var max = dist.RangeCounts.Values.Max();
                if (max > maxCount)
                    maxCount = max;
            }

            // 留10%的上方空间
            maxCount = (int)(maxCount * 1.1);

            // 绘制Y轴
            DrawYAxis(marginLeft, marginTop, chartHeight, maxCount);

            // 绘制X轴
            DrawXAxis(marginLeft, marginTop + chartHeight, chartWidth, ranges);

            // 绘制网格线
            DrawGridLines(marginLeft, marginTop, chartWidth, chartHeight, maxCount);

            // 绘制0%分界线（在Minus_9_0和Plus_0_10之间）
            DrawZeroPercentLine(marginLeft, marginTop, chartWidth, chartHeight, rangeCount);

            // 绘制每一天的折线
            for (int i = 0; i < _currentResult.DailyDistributions.Count; i++)
            {
                var distribution = _currentResult.DailyDistributions[i];
                DrawDistributionLine(distribution, ranges, marginLeft, marginTop, chartWidth, chartHeight, maxCount, i);
            }

            // 绘制图例
            DrawLegend(marginLeft, marginTop - 25, _currentResult.DailyDistributions);
        }

        /// <summary>
        /// 绘制Y轴
        /// </summary>
        private void DrawYAxis(double left, double top, double height, int maxValue)
        {
            // Y轴线
            var yAxis = new Line
            {
                X1 = left,
                Y1 = top,
                X2 = left,
                Y2 = top + height,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                StrokeThickness = 2
            };
            chartCanvas.Children.Add(yAxis);

            // Y轴刻度和标签（5个刻度）
            for (int i = 0; i <= 5; i++)
            {
                var value = maxValue * i / 5;
                var y = top + height - (height * i / 5);

                // 刻度标签
                var label = new TextBlock
                {
                    Text = value.ToString(),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
                };
                Canvas.SetLeft(label, left - 50);
                Canvas.SetTop(label, y - 10);
                chartCanvas.Children.Add(label);
            }

            // Y轴标题
            var yTitle = new TextBlock
            {
                Text = "合约数量",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            Canvas.SetLeft(yTitle, 5);
            Canvas.SetTop(yTitle, top + height / 2 - 10);
            chartCanvas.Children.Add(yTitle);
        }

        /// <summary>
        /// 绘制X轴
        /// </summary>
        private void DrawXAxis(double left, double top, double width, List<PriceChangeRange> ranges)
        {
            // X轴线
            var xAxis = new Line
            {
                X1 = left,
                Y1 = top,
                X2 = left + width,
                Y2 = top,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                StrokeThickness = 2
            };
            chartCanvas.Children.Add(xAxis);

            // X轴标签
            var rangeCount = ranges.Count;
            for (int i = 0; i < rangeCount; i++)
            {
                var x = left + (width * i / (rangeCount - 1));
                var name = DailyPriceChangeDistribution.GetRangeName(ranges[i]);

                var label = new TextBlock
                {
                    Text = name,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                    TextAlignment = TextAlignment.Center
                };

                // 标签显示在下方，不旋转
                Canvas.SetLeft(label, x - 30);
                Canvas.SetTop(label, top + 10);
                chartCanvas.Children.Add(label);
            }
        }

        /// <summary>
        /// 绘制网格线
        /// </summary>
        private void DrawGridLines(double left, double top, double width, double height, int maxValue)
        {
            // 水平网格线
            for (int i = 1; i < 5; i++)
            {
                var y = top + height - (height * i / 5);
                var line = new Line
                {
                    X1 = left,
                    Y1 = y,
                    X2 = left + width,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                chartCanvas.Children.Add(line);
            }
        }

        /// <summary>
        /// 绘制0%分界线（竖直虚线）
        /// </summary>
        private void DrawZeroPercentLine(double left, double top, double width, double height, int rangeCount)
        {
            // 0%的位置在第5个档位（Minus_9_0）和第6个档位（Plus_0_10）之间
            // 档位索引：0-11，0%在索引5和6之间，即5.5
            var x = left + (width * 5.5 / (rangeCount - 1));
            
            var line = new Line
            {
                X1 = x,
                Y1 = top,
                X2 = x,
                Y2 = top + height,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 150, 136)), // 青绿色
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 3 }
            };
            
            chartCanvas.Children.Add(line);
            
            // 添加"0%"标签
            var label = new TextBlock
            {
                Text = "0%",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)) // 半透明白色背景
            };
            Canvas.SetLeft(label, x - 12);
            Canvas.SetTop(label, top - 18);
            chartCanvas.Children.Add(label);
        }

        /// <summary>
        /// 绘制单条分布折线
        /// </summary>
        private void DrawDistributionLine(
            DailyPriceChangeDistribution distribution,
            List<PriceChangeRange> ranges,
            double marginLeft,
            double marginTop,
            double chartWidth,
            double chartHeight,
            int maxValue,
            int dayIndex)
        {
            var rangeCount = ranges.Count;
            var points = new PointCollection();

            // 计算每个点的坐标
            for (int i = 0; i < rangeCount; i++)
            {
                var range = ranges[i];
                var count = distribution.RangeCounts.ContainsKey(range) ? distribution.RangeCounts[range] : 0;

                var x = marginLeft + (chartWidth * i / (rangeCount - 1));
                var y = marginTop + chartHeight - (chartHeight * count / maxValue);

                points.Add(new Point(x, y));
            }

            // 绘制折线
            var polyline = new Polyline
            {
                Points = points,
                Stroke = GetLineColor(dayIndex),
                StrokeThickness = GetLineThickness(dayIndex),
                StrokeDashArray = GetLineDashArray(dayIndex)
            };

            chartCanvas.Children.Add(polyline);

            // 绘制数据点
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var ellipse = new Ellipse
                {
                    Width = GetPointSize(dayIndex),
                    Height = GetPointSize(dayIndex),
                    Fill = GetLineColor(dayIndex)
                };
                Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, point.Y - ellipse.Height / 2);
                chartCanvas.Children.Add(ellipse);

                // 为今天和昨天的数据点添加数字标签
                if (dayIndex == 0 || dayIndex == 1)
                {
                    var range = ranges[i];
                    var count = distribution.RangeCounts.ContainsKey(range) ? distribution.RangeCounts[range] : 0;

                    if (count > 0) // 只显示非0的数值
                    {
                        var label = new TextBlock
                        {
                            Text = count.ToString(),
                            FontSize = dayIndex == 0 ? 11 : 10,
                            FontWeight = FontWeights.Bold, // 今天和昨天都加粗
                            Foreground = dayIndex == 0 ? GetLineColor(dayIndex) : new SolidColorBrush(Colors.Black),
                            Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)) // 半透明白色背景
                        };
                        Canvas.SetLeft(label, point.X - 10);
                        Canvas.SetTop(label, point.Y - 22);
                        chartCanvas.Children.Add(label);
                    }
                }
            }
        }

        /// <summary>
        /// 获取折线颜色（今天最深，越远越浅）
        /// </summary>
        private Brush GetLineColor(int dayIndex)
        {
            return dayIndex switch
            {
                0 => new SolidColorBrush(Color.FromRgb(255, 87, 34)),   // 今天：深橙色
                1 => new SolidColorBrush(Color.FromRgb(30, 136, 229)),  // 昨天：深蓝色
                2 => new SolidColorBrush(Color.FromRgb(255, 193, 7)),   // 前天：黄色
                3 => new SolidColorBrush(Color.FromRgb(139, 195, 74)),  // 大前天：浅绿
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))  // 更早：灰色
            };
        }

        /// <summary>
        /// 获取折线粗细（今天最粗）
        /// </summary>
        private double GetLineThickness(int dayIndex)
        {
            return dayIndex switch
            {
                0 => 3.5,  // 今天
                1 => 2.5,  // 昨天
                2 => 2.0,  // 前天
                3 => 1.5,  // 大前天
                _ => 1.0   // 更早
            };
        }

        /// <summary>
        /// 获取折线样式（今天实线，越远越虚）
        /// </summary>
        private DoubleCollection? GetLineDashArray(int dayIndex)
        {
            return dayIndex switch
            {
                0 => null,                               // 今天：实线
                1 => null,                               // 昨天：实线
                2 => new DoubleCollection { 4, 2 },      // 前天：短虚线
                3 => new DoubleCollection { 6, 3 },      // 大前天：中虚线
                _ => new DoubleCollection { 8, 4 }       // 更早：长虚线
            };
        }

        /// <summary>
        /// 获取数据点大小
        /// </summary>
        private double GetPointSize(int dayIndex)
        {
            return dayIndex switch
            {
                0 => 6,  // 今天
                1 => 5,  // 昨天
                2 => 4,  // 前天
                _ => 3   // 更早
            };
        }

        /// <summary>
        /// 绘制图例
        /// </summary>
        private void DrawLegend(double left, double top, List<DailyPriceChangeDistribution> distributions)
        {
            var legendPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            for (int i = 0; i < distributions.Count; i++)
            {
                var dist = distributions[i];
                var legendItem = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 15, 0)
                };

                // 颜色块
                var colorBlock = new Rectangle
                {
                    Width = 20,
                    Height = 3,
                    Fill = GetLineColor(i),
                    VerticalAlignment = VerticalAlignment.Center
                };
                legendItem.Children.Add(colorBlock);

                // 日期标签
                var dateText = dist.Date.ToString("MM-dd");
                if (dist.IsToday)
                    dateText += " (今日)";

                var label = new TextBlock
                {
                    Text = dateText,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                legendItem.Children.Add(label);

                legendPanel.Children.Add(legendItem);
            }

            Canvas.SetLeft(legendPanel, left);
            Canvas.SetTop(legendPanel, top);
            chartCanvas.Children.Add(legendPanel);
        }
    }
}

