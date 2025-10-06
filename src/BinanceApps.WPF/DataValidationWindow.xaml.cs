using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 数据验证窗口
    /// </summary>
    public partial class DataValidationWindow : Window
    {
        private readonly AdvancedFilterResult _filterResult;
        private readonly List<BinanceApps.Core.Models.Kline> _klineData;
        private readonly int _analysisDays;
        private readonly decimal _volumeMultiplier;
        private readonly int _breakoutDays;

        public DataValidationWindow(AdvancedFilterResult filterResult, List<BinanceApps.Core.Models.Kline> klineData, 
                                 int analysisDays, decimal volumeMultiplier, int breakoutDays)
        {
            InitializeComponent();
            
            _filterResult = filterResult;
            _klineData = klineData;
            _analysisDays = analysisDays;
            _volumeMultiplier = volumeMultiplier;
            _breakoutDays = breakoutDays;
            
            // 设置窗口标题
            Title = $"数据验证 - {filterResult.Symbol}";
            
            // 初始化界面
            InitializeUI();
            
            // 加载数据
            LoadData();
        }

        /// <summary>
        /// 初始化界面
        /// </summary>
        private void InitializeUI()
        {
            // 设置标题和摘要
            txtTitle.Text = $"数据验证 - {_filterResult.Symbol}";
            txtSummary.Text = $"筛选条件：分析天数={_analysisDays}天，成交额倍数={_volumeMultiplier:F2}，突破天数={_breakoutDays}天";
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            try
            {
                // 加载K线数据
                LoadKlineData();
                
                // 加载成交额数据
                LoadVolumeData();
                
                // 加载统计信息
                LoadStatistics();
                
                // 绘制成交额趋势图
                DrawVolumeChart();
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载K线数据
        /// </summary>
        private void LoadKlineData()
        {
            if (_klineData == null || _klineData.Count == 0)
            {
                dgKlineData.ItemsSource = new List<KlineDisplayData>();
                return;
            }

            // 按日期排序，最新的在前面
            var sortedData = _klineData.OrderByDescending(k => k.OpenTime).ToList();
            
            // 转换为显示数据，计算位置比例
            var displayData = new List<KlineDisplayData>();
            if (sortedData.Count > 0)
            {
                var highestPrice = sortedData.Max(k => k.HighPrice);
                var lowestPrice = sortedData.Min(k => k.LowPrice);
                var priceRange = highestPrice - lowestPrice;
                
                foreach (var kline in sortedData)
                {
                    var locationRatio = priceRange > 0 ? (kline.ClosePrice - lowestPrice) / priceRange : 0;
                    
                    displayData.Add(new KlineDisplayData
                    {
                        Date = kline.OpenTime,
                        OpenPrice = kline.OpenPrice,
                        HighPrice = kline.HighPrice,
                        LowPrice = kline.LowPrice,
                        ClosePrice = kline.ClosePrice,
                        Volume = kline.Volume,
                        QuoteVolume = kline.QuoteVolume,
                        LocationRatio = locationRatio
                    });
                }
            }
            
            dgKlineData.ItemsSource = displayData;
        }

        /// <summary>
        /// 加载成交额数据
        /// </summary>
        private void LoadVolumeData()
        {
            if (_klineData == null || _klineData.Count == 0)
            {
                dgVolumeData.ItemsSource = new List<VolumeData>();
                dgVolumeStats.ItemsSource = new List<VolumeStatsData>();
                return;
            }

            // 计算成交额数据
            var volumeData = new List<VolumeData>();
            var volumeStats = new List<VolumeStatsData>();

            // 按日期排序
            var sortedKlines = _klineData.OrderBy(k => k.OpenTime).ToList();
            
            if (sortedKlines.Count >= _analysisDays)
            {
                // 计算前N天的成交额均值
                var previousDays = sortedKlines.Take(_analysisDays - 1).ToList();
                var averageVolume = previousDays.Average(k => k.QuoteVolume);
                
                // 最新一天的成交额
                var latestVolume = sortedKlines.Last().QuoteVolume;
                
                // 计算倍数
                var multiplier = averageVolume > 0 ? latestVolume / averageVolume : 0;

                // 填充成交额明细
                foreach (var kline in sortedKlines)
                {
                    var relativeMultiplier = averageVolume > 0 ? kline.QuoteVolume / averageVolume : 0;
                    var averagePrice = kline.Volume > 0 ? kline.QuoteVolume / kline.Volume : 0;
                    
                    volumeData.Add(new VolumeData
                    {
                        Date = kline.OpenTime,
                        QuoteVolume = kline.QuoteVolume,
                        Volume = kline.Volume,
                        AveragePrice = averagePrice,
                        RelativeMultiplier = relativeMultiplier
                    });
                }

                // 填充成交额统计
                foreach (var kline in sortedKlines)
                {
                    var comparison = averageVolume > 0 ? kline.QuoteVolume - averageVolume : 0;
                    var dayMultiplier = averageVolume > 0 ? kline.QuoteVolume / averageVolume : 0;
                    var status = dayMultiplier >= _volumeMultiplier ? "达标" : "未达标";
                    
                    volumeStats.Add(new VolumeStatsData
                    {
                        Date = kline.OpenTime,
                        QuoteVolume = kline.QuoteVolume,
                        Comparison = comparison,
                        Multiplier = dayMultiplier,
                        Status = status
                    });
                }

                // 更新统计信息
                txtAverageVolume.Text = $"{averageVolume:F2}";
                txtCurrentVolume.Text = $"{latestVolume:F2}";
                txtMultiplier.Text = $"{multiplier:F2}";
            }

            dgVolumeData.ItemsSource = volumeData;
            dgVolumeStats.ItemsSource = volumeStats;
        }

        /// <summary>
        /// 加载统计信息
        /// </summary>
        private void LoadStatistics()
        {
            if (_klineData == null || _klineData.Count == 0) return;

            var sortedKlines = _klineData.OrderBy(k => k.OpenTime).ToList();
            var latestKline = sortedKlines.Last();
            
            // 最新收盘价
            txtLatestClose.Text = $"{latestKline.ClosePrice:F8}";
            
            // 最近X天最高价
            var recentKlines = sortedKlines.TakeLast(_analysisDays).ToList();
            var recentHigh = recentKlines.Max(k => k.HighPrice);
            txtRecentHigh.Text = $"{recentHigh:F8}";
            
            // 最新成交额
            txtLatestVolume.Text = $"{latestKline.QuoteVolume:F2}";
            
            // 成交额倍数
            if (sortedKlines.Count >= _analysisDays)
            {
                var previousDays = sortedKlines.Take(_analysisDays - 1).ToList();
                var averageVolume = previousDays.Average(k => k.QuoteVolume);
                var multiplier = averageVolume > 0 ? latestKline.QuoteVolume / averageVolume : 0;
                txtVolumeMultiplier.Text = $"{multiplier:F2}";
            }
        }

        /// <summary>
        /// 绘制成交额趋势图
        /// </summary>
        private void DrawVolumeChart()
        {
            if (_klineData == null || _klineData.Count == 0) return;

            try
            {
                canvasVolumeChart.Children.Clear();
                
                var sortedKlines = _klineData.OrderBy(k => k.OpenTime).ToList();
                var canvasWidth = canvasVolumeChart.ActualWidth;
                var canvasHeight = canvasVolumeChart.ActualHeight;
                
                if (canvasWidth <= 0 || canvasHeight <= 0) return;
                
                // 计算数据范围
                var maxVolume = sortedKlines.Max(k => k.QuoteVolume);
                var minVolume = sortedKlines.Min(k => k.QuoteVolume);
                var volumeRange = maxVolume - minVolume;
                
                if (volumeRange <= 0) return;
                
                // 绘制坐标轴
                var axisBrush = new SolidColorBrush(Colors.Black);
                var axisPen = new Pen(axisBrush, 1);
                
                // Y轴
                var yAxis = new Line
                {
                    X1 = 30, Y1 = 20,
                    X2 = 30, Y2 = canvasHeight - 20,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                canvasVolumeChart.Children.Add(yAxis);
                
                // X轴
                var xAxis = new Line
                {
                    X1 = 30, Y1 = canvasHeight - 20,
                    X2 = canvasWidth - 20, Y2 = canvasHeight - 20,
                    Stroke = axisBrush,
                    StrokeThickness = 1
                };
                canvasVolumeChart.Children.Add(xAxis);
                
                // 绘制成交额曲线
                var lineBrush = new SolidColorBrush(Colors.Blue);
                var linePen = new Pen(lineBrush, 2);
                
                var points = new List<Point>();
                var xStep = (canvasWidth - 50) / (sortedKlines.Count - 1);
                
                for (int i = 0; i < sortedKlines.Count; i++)
                {
                    var kline = sortedKlines[i];
                    var x = 30 + (double)i * xStep;
                    var y = canvasHeight - 20 - ((double)(kline.QuoteVolume - minVolume) / (double)volumeRange) * (canvasHeight - 40);
                    
                    points.Add(new Point(x, y));
                    
                    // 绘制数据点
                    var dataPoint = new Ellipse
                    {
                        Width = 4,
                        Height = 4,
                        Fill = lineBrush
                    };
                    Canvas.SetLeft(dataPoint, x - 2);
                    Canvas.SetTop(dataPoint, y - 2);
                    canvasVolumeChart.Children.Add(dataPoint);
                }
                
                // 绘制连接线
                for (int i = 1; i < points.Count; i++)
                {
                    var line = new Line
                    {
                        X1 = points[i - 1].X,
                        Y1 = points[i - 1].Y,
                        X2 = points[i].X,
                        Y2 = points[i].Y,
                        Stroke = lineBrush,
                        StrokeThickness = 2
                    };
                    canvasVolumeChart.Children.Add(line);
                }
                
                // 添加标签
                var labelBrush = new SolidColorBrush(Colors.Gray);
                
                // Y轴标签
                var yLabel = new TextBlock
                {
                    Text = "成交额",
                    FontSize = 10,
                    Foreground = labelBrush
                };
                Canvas.SetLeft(yLabel, 5);
                Canvas.SetTop(yLabel, 10);
                canvasVolumeChart.Children.Add(yLabel);
                
                // 最大值标签
                var maxLabel = new TextBlock
                {
                    Text = $"{maxVolume:F0}",
                    FontSize = 8,
                    Foreground = labelBrush
                };
                Canvas.SetLeft(maxLabel, 5);
                Canvas.SetTop(maxLabel, 20);
                canvasVolumeChart.Children.Add(maxLabel);
                
                // 最小值标签
                var minLabel = new TextBlock
                {
                    Text = $"{minVolume:F0}",
                    FontSize = 8,
                    Foreground = labelBrush
                };
                Canvas.SetLeft(minLabel, 5);
                Canvas.SetTop(minLabel, canvasHeight - 30);
                canvasVolumeChart.Children.Add(minLabel);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"绘制成交额趋势图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出数据按钮点击事件
        /// </summary>
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    FileName = $"{_filterResult.Symbol}_数据验证_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ExportToCSV(saveFileDialog.FileName);
                    MessageBox.Show($"数据已成功导出到: {saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出数据到CSV
        /// </summary>
        private void ExportToCSV(string filePath)
        {
            var csv = new StringBuilder();
            
            // 添加标题
            csv.AppendLine($"数据验证报告 - {_filterResult.Symbol}");
            csv.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"筛选条件: 分析天数={_analysisDays}天，成交额倍数={_volumeMultiplier:F2}，突破天数={_breakoutDays}天");
            csv.AppendLine();
            
            // 添加统计信息
            csv.AppendLine("统计信息");
            csv.AppendLine($"最新收盘价,{txtLatestClose.Text}");
            csv.AppendLine($"最近{_analysisDays}天最高价,{txtRecentHigh.Text}");
            csv.AppendLine($"最新成交额,{txtLatestVolume.Text}");
            csv.AppendLine($"成交额倍数,{txtVolumeMultiplier.Text}");
            csv.AppendLine();
            
            // 添加K线数据
            csv.AppendLine("K线数据");
            csv.AppendLine("日期,开盘价,最高价,最低价,收盘价,成交量,成交额,位置比例");
            if (_klineData != null)
            {
                var sortedKlines = _klineData.OrderBy(k => k.OpenTime).ToList();
                foreach (var kline in sortedKlines)
                {
                    csv.AppendLine($"{kline.OpenTime:yyyy-MM-dd},{kline.OpenPrice:F8},{kline.HighPrice:F8},{kline.LowPrice:F8},{kline.ClosePrice:F8},{kline.Volume:F2},{kline.QuoteVolume:F2}");
                }
            }
            csv.AppendLine();
            
            // 添加成交额统计
            csv.AppendLine("成交额统计");
            csv.AppendLine($"前{_analysisDays-1}天成交额均值,{txtAverageVolume.Text}");
            csv.AppendLine($"最新成交额,{txtCurrentVolume.Text}");
            csv.AppendLine($"倍数,{txtMultiplier.Text}");
            
            // 写入文件
            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 窗口大小改变事件
        /// </summary>
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            // 重新绘制成交额趋势图
            if (canvasVolumeChart != null)
            {
                DrawVolumeChart();
            }
        }
    }

    /// <summary>
    /// 成交额数据
    /// </summary>
    public class VolumeData
    {
        public DateTime Date { get; set; }
        public decimal QuoteVolume { get; set; }
        public decimal Volume { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal RelativeMultiplier { get; set; }
    }

    /// <summary>
    /// 成交额统计数据
    /// </summary>
    public class VolumeStatsData
    {
        public DateTime Date { get; set; }
        public decimal QuoteVolume { get; set; }
        public decimal Comparison { get; set; }
        public decimal Multiplier { get; set; }
        public string Status { get; set; } = "";
    }
    
    /// <summary>
    /// K线显示数据
    /// </summary>
    public class KlineDisplayData
    {
        public DateTime Date { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
        public decimal QuoteVolume { get; set; }
        public decimal LocationRatio { get; set; }
    }
} 