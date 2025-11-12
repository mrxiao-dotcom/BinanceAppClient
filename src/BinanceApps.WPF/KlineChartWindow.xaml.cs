using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using BinanceApps.Core.Models;
using LiveChartsCore.Kernel.Sketches;

namespace BinanceApps.WPF
{
    /// <summary>
    /// K线和EMA数据图表窗口
    /// </summary>
    public partial class KlineChartWindow : Window
    {
        private List<DateTime> _timePoints = new List<DateTime>();
        
        public ISeries[] Series { get; set; } = Array.Empty<ISeries>();
        public Axis[] XAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] YAxes { get; set; } = Array.Empty<Axis>();
        
        public ISeries[] VolumeSeries { get; set; } = Array.Empty<ISeries>();
        public Axis[] VolumeXAxes { get; set; } = Array.Empty<Axis>();
        public Axis[] VolumeYAxes { get; set; } = Array.Empty<Axis>();

        public KlineChartWindow(string symbol, HourlyKlineData klineData)
        {
            InitializeComponent();
            
            // 先设置DataContext
            DataContext = this;
            
            // 加载图表数据
            bool loadSuccess = LoadChartData(symbol, klineData);
            
            // 如果加载失败，标记窗口需要关闭
            if (!loadSuccess)
            {
                // 在Loaded事件中关闭，避免构造函数中关闭导致问题
                Loaded += (s, e) =>
                {
                    MessageBox.Show("没有可显示的K线数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                };
            }
        }

        private bool LoadChartData(string symbol, HourlyKlineData klineData)
        {
            try
            {
                // 按时间排序
                var sortedKlines = klineData.Klines.OrderBy(k => k.OpenTime).ToList();
                var sortedEma = klineData.EmaValues.OrderBy(e => e.Key).ToList();

                if (sortedKlines.Count == 0)
                {
                    Console.WriteLine($"⚠️ {symbol} 没有K线数据");
                    return false;
                }

                // 设置标题
                txtTitle.Text = $"{symbol} - K线与EMA数据图表";
                var emaStatus = klineData.EmaValues.Count > 0 ? "已计算" : "未计算";
                
                // 转换时间为本地时间显示
                var firstTime = sortedKlines.First().OpenTime;
                var lastTime = sortedKlines.Last().OpenTime;
                var firstLocal = firstTime.Kind == DateTimeKind.Utc ? firstTime.ToLocalTime() : firstTime;
                var lastLocal = lastTime.Kind == DateTimeKind.Utc ? lastTime.ToLocalTime() : lastTime;
                
                txtInfo.Text = $"EMA周期: {emaStatus} | " +
                               $"时间范围: {firstLocal:yyyy-MM-dd HH:mm} ~ {lastLocal:yyyy-MM-dd HH:mm}";

                // 准备数据点 - 使用索引作为X轴
                var klineValues = new List<double>();
                var emaValuesList = new List<double>();
                
                // 存储时间点用于标签显示（转换为北京时间）
                _timePoints.Clear();
                foreach (var kline in sortedKlines)
                {
                    // 转换为本地时间（北京时间 UTC+8）
                    var localTime = kline.OpenTime.Kind == DateTimeKind.Utc 
                        ? kline.OpenTime.ToLocalTime() 
                        : kline.OpenTime;
                    _timePoints.Add(localTime);
                    klineValues.Add((double)kline.ClosePrice);
                }

                // 为EMA数据匹配索引 - 将EMA字典转换为更高效的查找
                Console.WriteLine($"📊 开始匹配EMA数据，K线数量={sortedKlines.Count}，EMA字典数量={klineData.EmaValues.Count}");
                var emaDict = klineData.EmaValues; // 使用原始字典，效率更高
                
                // 输出前几个K线时间和EMA时间用于对比
                Console.WriteLine($"🔍 前3个K线时间:");
                for (int i = 0; i < Math.Min(3, sortedKlines.Count); i++)
                {
                    Console.WriteLine($"  K线[{i}]: {sortedKlines[i].OpenTime:yyyy-MM-dd HH:mm:ss}");
                }
                Console.WriteLine($"🔍 前3个EMA时间:");
                var emaKeys = emaDict.Keys.OrderBy(k => k).Take(3).ToList();
                foreach (var key in emaKeys)
                {
                    Console.WriteLine($"  EMA时间: {key:yyyy-MM-dd HH:mm:ss}, 值={emaDict[key]:F8}");
                }
                
                int matchedCount = 0;
                for (int i = 0; i < sortedKlines.Count; i++)
                {
                    var kline = sortedKlines[i];
                    // 尝试从字典中查找EMA值
                    if (emaDict.ContainsKey(kline.OpenTime))
                    {
                        var emaValue = emaDict[kline.OpenTime];
                        emaValuesList.Add((double)emaValue);
                        matchedCount++;
                        
                        // 输出前3个匹配的EMA
                        if (matchedCount <= 3)
                        {
                            Console.WriteLine($"✓ 匹配[{i}]: K线时间={kline.OpenTime:yyyy-MM-dd HH:mm:ss}, EMA={emaValue:F8}");
                        }
                    }
                    else
                    {
                        // 没有匹配的EMA，添加NaN（不显示）
                        emaValuesList.Add(double.NaN);
                    }
                }
                
                Console.WriteLine($"✅ EMA数据匹配完成，K线={klineValues.Count}，匹配EMA={matchedCount}/{emaDict.Count}");
                Console.WriteLine($"📊 EMA值列表大小={emaValuesList.Count}, 其中有效值={matchedCount}");

                // 创建系列
                var seriesList = new List<ISeries>
                {
                    new LineSeries<double>
                    {
                        Name = "K线Close",
                        Values = klineValues,
                        Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                        Fill = null,
                        GeometrySize = 0,
                        LineSmoothness = 0
                    }
                };

                // 只在有EMA数据时添加EMA系列
                if (matchedCount > 0)
                {
                    Console.WriteLine($"📈 添加EMA系列到图表，共 {matchedCount} 个有效值");
                    
                    // 输出EMA值列表的统计信息
                    var validEmaCount = emaValuesList.Count(v => !double.IsNaN(v));
                    var nanCount = emaValuesList.Count(v => double.IsNaN(v));
                    Console.WriteLine($"📊 EMA列表统计: 总数={emaValuesList.Count}, 有效={validEmaCount}, NaN={nanCount}");
                    
                    // 输出前几个和后几个EMA值
                    Console.WriteLine($"🔍 前3个EMA值:");
                    for (int i = 0; i < Math.Min(3, emaValuesList.Count); i++)
                    {
                        var val = emaValuesList[i];
                        Console.WriteLine($"  [{i}] = {(double.IsNaN(val) ? "NaN" : val.ToString("F8"))}");
                    }
                    Console.WriteLine($"🔍 后3个EMA值:");
                    for (int i = Math.Max(0, emaValuesList.Count - 3); i < emaValuesList.Count; i++)
                    {
                        var val = emaValuesList[i];
                        Console.WriteLine($"  [{i}] = {(double.IsNaN(val) ? "NaN" : val.ToString("F8"))}");
                    }
                    
                    // 尝试方法1：直接使用emaValuesList（包含NaN）
                    var emaSeries = new LineSeries<double>
                    {
                        Name = "EMA",
                        Values = emaValuesList,
                        Stroke = new SolidColorPaint(SKColors.OrangeRed) { StrokeThickness = 3 }, // 加粗线条
                        Fill = null,
                        GeometrySize = 5, // 显示数据点以便调试
                        LineSmoothness = 0,
                        IsVisible = true
                    };
                    
                    seriesList.Add(emaSeries);
                    Console.WriteLine($"✅ EMA系列已添加到列表，系列总数={seriesList.Count}");
                }
                else
                {
                    Console.WriteLine("⚠️ 没有匹配到任何EMA数据");
                }
                
                Series = seriesList.ToArray();
                Console.WriteLine($"📊 图表系列数组已设置，共 {Series.Length} 个系列");
                for (int i = 0; i < Series.Length; i++)
                {
                    var series = Series[i];
                    Console.WriteLine($"  系列[{i}]: Name={series.Name}");
                }

                // 配置X轴（使用索引，标签显示时间）
                var sampleStep = Math.Max(1, sortedKlines.Count / 10); // 显示大约10个标签
                XAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "时间",
                        NamePaint = new SolidColorPaint(SKColors.Black),
                        LabelsPaint = new SolidColorPaint(SKColors.Gray),
                        Labeler = value =>
                        {
                            var index = (int)value;
                            if (index >= 0 && index < _timePoints.Count)
                            {
                                return _timePoints[index].ToString("MM-dd HH:mm");
                            }
                            return string.Empty;
                        },
                        LabelsRotation = 15,
                        MinStep = sampleStep
                    }
                };

                // 配置Y轴（价格轴）
                YAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "价格",
                        NamePaint = new SolidColorPaint(SKColors.Black),
                        LabelsPaint = new SolidColorPaint(SKColors.Gray),
                        Labeler = value => value.ToString("F8")
                    }
                };

                // 更新统计信息
                UpdateStatistics(sortedKlines, sortedEma, klineData);

                // 加载成交额副图数据
                LoadVolumeChart(sortedKlines);

                Console.WriteLine($"✅ 图表加载完成：{symbol}，K线数量={sortedKlines.Count}，EMA数量={sortedEma.Count}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载图表数据失败: {ex.Message}");
                MessageBox.Show($"加载图表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void LoadVolumeChart(List<Kline> sortedKlines)
        {
            try
            {
                // 准备成交额数据
                var volumeValues = new List<double>();
                foreach (var kline in sortedKlines)
                {
                    // 将成交额转换为USDT（假设QuoteVolume已经是USDT）
                    volumeValues.Add((double)kline.QuoteVolume);
                }

                // 创建成交额柱状图系列
                VolumeSeries = new ISeries[]
                {
                    new ColumnSeries<double>
                    {
                        Name = "成交额(USDT)",
                        Values = volumeValues,
                        Fill = new SolidColorPaint(SKColors.LightBlue.WithAlpha(180)),
                        Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 1 },
                        MaxBarWidth = 20
                    }
                };

                // 配置成交额图表的X轴（与主图保持一致）
                var sampleStep = Math.Max(1, sortedKlines.Count / 10);
                VolumeXAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "时间",
                        NamePaint = new SolidColorPaint(SKColors.Black),
                        LabelsPaint = new SolidColorPaint(SKColors.Gray),
                        Labeler = value =>
                        {
                            var index = (int)value;
                            if (index >= 0 && index < _timePoints.Count)
                            {
                                return _timePoints[index].ToString("MM-dd HH:mm");
                            }
                            return string.Empty;
                        },
                        LabelsRotation = 15,
                        MinStep = sampleStep
                    }
                };

                // 配置成交额图表的Y轴
                VolumeYAxes = new Axis[]
                {
                    new Axis
                    {
                        Name = "成交额(USDT)",
                        NamePaint = new SolidColorPaint(SKColors.Black),
                        LabelsPaint = new SolidColorPaint(SKColors.Gray),
                        Labeler = value =>
                        {
                            if (value >= 1_000_000)
                                return $"{value / 1_000_000:F1}M";
                            else if (value >= 1_000)
                                return $"{value / 1_000:F1}K";
                            else
                                return value.ToString("F0");
                        }
                    }
                };

                Console.WriteLine($"📊 成交额副图加载完成，数据点数={volumeValues.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载成交额副图失败: {ex.Message}");
            }
        }

        private void UpdateStatistics(List<Kline> klines, List<KeyValuePair<DateTime, decimal>> emaValues, HourlyKlineData klineData)
        {
            // K线数量
            txtKlineCount.Text = klines.Count.ToString();

            // 最新收盘价
            var lastClose = klines.Last().ClosePrice;
            txtLastClose.Text = lastClose.ToString("F8");

            // 当前EMA
            if (emaValues.Count > 0)
            {
                var currentEma = emaValues.Last().Value;
                txtCurrentEma.Text = currentEma.ToString("F8");

                // 距离EMA
                var distance = currentEma != 0 ? ((lastClose - currentEma) / currentEma * 100) : 0;
                txtDistance.Text = $"{distance:F2}%";
                txtDistance.Foreground = distance >= 0 ? Brushes.Green : Brushes.Red;
            }
            else
            {
                txtCurrentEma.Text = "未计算";
                txtDistance.Text = "-";
            }

            // 连续数量
            var aboveCount = klineData.AboveEmaCount;
            var belowCount = klineData.BelowEmaCount;
            
            if (aboveCount > 0)
            {
                txtContinuous.Text = $"大于 {aboveCount}";
                txtContinuous.Foreground = Brushes.Green;
            }
            else if (belowCount > 0)
            {
                txtContinuous.Text = $"小于 {belowCount}";
                txtContinuous.Foreground = Brushes.Red;
            }
            else
            {
                txtContinuous.Text = "0";
                txtContinuous.Foreground = Brushes.Gray;
            }
        }
    }
}

