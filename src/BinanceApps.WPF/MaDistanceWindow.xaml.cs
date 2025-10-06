using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BinanceApps.Core.Models;
using BinanceApps.Core.Services;
using Microsoft.Extensions.Logging;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 参数配置
    /// </summary>
    public class MaDistanceParameters
    {
        public int Period { get; set; } = 20;
        public decimal Threshold { get; set; } = 5m;
    }
    
    public partial class MaDistanceWindow : Window
    {
        private readonly ILogger<MaDistanceWindow> _logger;
        private readonly MaDistanceService _maDistanceService;
        private MaDistanceAnalysisResult? _currentResult;
        private readonly string _parametersFilePath;
        
        // 排序状态
        private string _currentSortColumn = ""; // Distance, Price, Change
        private bool _sortAscending = false;
        private MaDistanceZone _currentSortZone;
        
        public MaDistanceWindow(ILogger<MaDistanceWindow> logger, MaDistanceService maDistanceService)
        {
            InitializeComponent();
            _logger = logger;
            _maDistanceService = maDistanceService;
            
            // 参数文件路径
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BinanceApps"
            );
            Directory.CreateDirectory(appDataPath);
            _parametersFilePath = Path.Combine(appDataPath, "ma_distance_params.json");
        }
        
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("均线距离分析窗口已加载");
            
            // 加载保存的参数
            LoadParameters();
            
            // 尝试加载今天的历史数据
            await TryLoadTodayDataAsync();
        }
        
        /// <summary>
        /// 加载保存的参数
        /// </summary>
        private void LoadParameters()
        {
            try
            {
                if (File.Exists(_parametersFilePath))
                {
                    var json = File.ReadAllText(_parametersFilePath);
                    var parameters = JsonSerializer.Deserialize<MaDistanceParameters>(json);
                    if (parameters != null)
                    {
                        txtPeriod.Text = parameters.Period.ToString();
                        txtThreshold.Text = parameters.Threshold.ToString();
                        _logger.LogInformation($"已加载参数：周期={parameters.Period}, 阈值={parameters.Threshold}%");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载参数失败，使用默认值");
            }
        }
        
        /// <summary>
        /// 保存参数
        /// </summary>
        private void SaveParameters(int period, decimal threshold)
        {
            try
            {
                var parameters = new MaDistanceParameters
                {
                    Period = period,
                    Threshold = threshold
                };
                
                var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(_parametersFilePath, json);
                _logger.LogInformation($"已保存参数：周期={period}, 阈值={threshold}%");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存参数失败");
            }
        }
        
        /// <summary>
        /// 尝试加载今天的历史数据
        /// </summary>
        private async Task TryLoadTodayDataAsync()
        {
            try
            {
                var period = int.Parse(txtPeriod.Text);
                var threshold = decimal.Parse(txtThreshold.Text);
                
                if (await _maDistanceService.HasDataForDateAsync(DateTime.Today, period, threshold))
                {
                    txtStatus.Text = "正在加载今天的缓存数据...";
                    // 注意：这里需要重新计算，因为我们没有保存详细结果
                    // 或者修改服务层保存详细结果
                    await CalculateAsync();
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        
        /// <summary>
        /// 计算按钮点击
        /// </summary>
        private async void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            await CalculateAsync();
        }
        
        /// <summary>
        /// 执行计算
        /// </summary>
        private async Task CalculateAsync()
        {
            try
            {
                // 1. 验证输入
                if (!int.TryParse(txtPeriod.Text, out int period) || period < 1 || period > 100)
                {
                    MessageBox.Show("请输入有效的N天均线（1-100）", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!decimal.TryParse(txtThreshold.Text, out decimal threshold) || threshold < 1 || threshold > 50)
                {
                    MessageBox.Show("请输入有效的距离百分比（1-50）", "输入错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 2. 禁用按钮，显示进度
                btnCalculate.IsEnabled = false;
                txtStatus.Text = "正在计算均线距离...";
                
                // 3. 执行计算
                var result = await Task.Run(async () => 
                    await _maDistanceService.CalculateMaDistanceAsync(DateTime.Today, period, threshold));
                
                _currentResult = result;
                
                // 4. 保存今天的结果
                await _maDistanceService.SaveAnalysisResultAsync(result);
                
                // 5. 生成过去30天的历史数据（如果不存在）
                txtStatus.Text = "正在生成历史数据...";
                await GenerateHistoricalDataAsync(period, threshold);
                
                // 6. 显示结果
                DisplayResult(result);
                
                // 7. 加载历史分布（现在应该有30天的数据）
                await LoadHistoryDistributionsAsync(period, threshold);
                
                // 8. 保存参数供下次使用
                SaveParameters(period, threshold);
                
                txtStatus.Text = $"计算完成：{result.AllData.Count} 个合约";
                _logger.LogInformation($"计算完成：{result.AllData.Count} 个合约");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "计算失败");
                MessageBox.Show($"计算失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "计算失败";
            }
            finally
            {
                btnCalculate.IsEnabled = true;
            }
        }
        
        /// <summary>
        /// 显示计算结果
        /// </summary>
        private void DisplayResult(MaDistanceAnalysisResult result)
        {
            // 更新计数
            txtAboveNearCount.Text = $"({result.AboveNear.Count}个)";
            txtAboveFarCount.Text = $"({result.AboveFar.Count}个)";
            txtBelowNearCount.Text = $"({result.BelowNear.Count}个)";
            txtBelowFarCount.Text = $"({result.BelowFar.Count}个)";
            
            // 更新描述文本显示实际百分比
            var threshold = result.ThresholdPercent;
            txtAboveNearDesc.Text = $"距离 ≤ {threshold}%";
            txtAboveFarDesc.Text = $"距离 > {threshold}%";
            txtBelowNearDesc.Text = $"距离 ≥ -{threshold}%";
            txtBelowFarDesc.Text = $"距离 < -{threshold}%";
            
            // 显示各象限数据
            DisplayQuadrantData(panelAboveNear, result.AboveNear, MaDistanceZone.AboveNear);
            DisplayQuadrantData(panelAboveFar, result.AboveFar, MaDistanceZone.AboveFar);
            DisplayQuadrantData(panelBelowNear, result.BelowNear, MaDistanceZone.BelowNear);
            DisplayQuadrantData(panelBelowFar, result.BelowFar, MaDistanceZone.BelowFar);
        }
        
        /// <summary>
        /// 显示象限数据
        /// </summary>
        private void DisplayQuadrantData(StackPanel panel, List<MaDistanceData> data, MaDistanceZone zone)
        {
            panel.Children.Clear();
            
            if (data.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "暂无数据",
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Margin = new Thickness(0, 20, 0, 0)
                };
                panel.Children.Add(emptyText);
                return;
            }
            
            // 添加表头
            panel.Children.Add(CreateTableHeader(zone));
            
            // 应用排序
            var sortedData = ApplySorting(data, zone);
            
            // 显示数据行
            int index = 1;
            foreach (var item in sortedData)
            {
                panel.Children.Add(CreateDataRow(item, index, zone));
                index++;
            }
        }
        
        /// <summary>
        /// 创建表头
        /// </summary>
        private Border CreateTableHeader(MaDistanceZone zone)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 3)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });  // 序号
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 合约
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 价格
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 涨幅
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });  // 距离
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // 成交额
            
            // 序号
            AddHeaderText(grid, "#", 0, false, null, zone);
            
            // 合约
            AddHeaderText(grid, "合约", 1, false, null, zone);
            
            // 价格（可排序）
            AddHeaderText(grid, "价格", 2, true, "Price", zone);
            
            // 涨幅（可排序）
            AddHeaderText(grid, "涨幅", 3, true, "Change", zone);
            
            // 距离（可排序）
            AddHeaderText(grid, "距离", 4, true, "Distance", zone);
            
            // 成交额（可排序）
            AddHeaderText(grid, "24H成交额", 5, true, "Volume", zone);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 添加表头文本
        /// </summary>
        private void AddHeaderText(Grid grid, string text, int column, bool sortable, string? sortColumn, MaDistanceZone zone)
        {
            var sortIndicator = "";
            if (sortable && _currentSortColumn == sortColumn && _currentSortZone == zone)
            {
                sortIndicator = _sortAscending ? " ▲" : " ▼";
            }
            
            var textBlock = new TextBlock
            {
                Text = text + sortIndicator,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = sortable 
                    ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
                    : new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = sortable ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                ToolTip = sortable ? "点击排序" : null
            };
            
            if (sortable && sortColumn != null)
            {
                textBlock.MouseDown += (s, e) => OnHeaderClick(sortColumn, zone);
            }
            
            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }
        
        /// <summary>
        /// 表头点击排序
        /// </summary>
        private void OnHeaderClick(string columnName, MaDistanceZone zone)
        {
            // 切换排序
            if (_currentSortColumn == columnName && _currentSortZone == zone)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _currentSortColumn = columnName;
                _currentSortZone = zone;
                _sortAscending = false; // 默认降序
            }
            
            // 刷新显示
            if (_currentResult != null)
            {
                DisplayResult(_currentResult);
            }
        }
        
        /// <summary>
        /// 应用排序
        /// </summary>
        private List<MaDistanceData> ApplySorting(List<MaDistanceData> data, MaDistanceZone zone)
        {
            if (string.IsNullOrEmpty(_currentSortColumn) || _currentSortZone != zone)
            {
                // 默认按距离降序（绝对值）
                return data.OrderByDescending(d => Math.Abs(d.DistancePercent)).ToList();
            }
            
            IEnumerable<MaDistanceData> sorted = data;
            
            switch (_currentSortColumn)
            {
                case "Distance":
                    sorted = _sortAscending
                        ? data.OrderBy(d => d.DistancePercent)
                        : data.OrderByDescending(d => d.DistancePercent);
                    break;
                case "Price":
                    sorted = _sortAscending
                        ? data.OrderBy(d => d.CurrentPrice)
                        : data.OrderByDescending(d => d.CurrentPrice);
                    break;
                case "Change":
                    sorted = _sortAscending
                        ? data.OrderBy(d => d.PriceChangePercent)
                        : data.OrderByDescending(d => d.PriceChangePercent);
                    break;
                case "Volume":
                    sorted = _sortAscending
                        ? data.OrderBy(d => d.QuoteVolume)
                        : data.OrderByDescending(d => d.QuoteVolume);
                    break;
            }
            
            return sorted.ToList();
        }
        
        /// <summary>
        /// 创建数据行
        /// </summary>
        private Border CreateDataRow(MaDistanceData data, int index, MaDistanceZone zone)
        {
            var changeColor = data.PriceChangePercent > 0 ? Colors.Green
                : data.PriceChangePercent < 0 ? Colors.Red : Colors.Gray;
            
            var distanceColor = data.DistancePercent > 0 ? Colors.Green : Colors.Red;
            
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Colors.White)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            
            // 序号
            AddCellText(grid, index.ToString(), 0, Colors.Gray);
            
            // 合约
            var symbolText = new TextBlock
            {
                Text = data.Symbol,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "双击复制"
            };
            symbolText.MouseDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    try
                    {
                        Clipboard.SetText(data.Symbol);
                        var originalColor = symbolText.Foreground;
                        symbolText.Foreground = new SolidColorBrush(Color.FromRgb(0, 150, 0));
                        
                        var timer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(500)
                        };
                        timer.Tick += (ts, te) =>
                        {
                            symbolText.Foreground = originalColor;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                    catch { }
                }
            };
            Grid.SetColumn(symbolText, 1);
            grid.Children.Add(symbolText);
            
            // 价格
            AddCellText(grid, $"${data.CurrentPrice:F4}", 2, Colors.Black);
            
            // 涨幅
            var changeText = $"{(data.PriceChangePercent >= 0 ? "+" : "")}{data.PriceChangePercent:F2}%";
            AddCellText(grid, changeText, 3, changeColor, true);
            
            // 距离
            var distanceText = $"{(data.DistancePercent >= 0 ? "+" : "")}{data.DistancePercent:F2}%";
            AddCellText(grid, distanceText, 4, distanceColor, true);
            
            // 成交额
            var volumeText = data.QuoteVolume >= 1_000_000m
                ? $"${data.QuoteVolume / 1_000_000m:F1}M"
                : $"${data.QuoteVolume / 1000m:F0}K";
            AddCellText(grid, volumeText, 5, Color.FromRgb(80, 80, 80));
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 添加单元格文本
        /// </summary>
        private void AddCellText(Grid grid, string text, int column, Color color, bool bold = false)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(color),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }
        
        /// <summary>
        /// 加载历史分布数据
        /// </summary>
        private async Task LoadHistoryDistributionsAsync(int period, decimal threshold)
        {
            try
            {
                var distributions = await _maDistanceService.GetHistoryDistributionsAsync(period, threshold, 30);
                
                panelHistory.Children.Clear();
                
                if (distributions.Count == 0)
                {
                    var emptyText = new TextBlock
                    {
                        Text = "暂无历史数据",
                        TextAlignment = TextAlignment.Center,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    panelHistory.Children.Add(emptyText);
                    return;
                }
                
                // 添加表头
                panelHistory.Children.Add(CreateHistoryHeader(threshold));
                
                // 计算成交额的最大值和最小值（用于热力图）
                decimal minVolume = distributions.Min(d => d.TotalQuoteVolume);
                decimal maxVolume = distributions.Max(d => d.TotalQuoteVolume);
                
                // 显示每日数据
                foreach (var dist in distributions)
                {
                    panelHistory.Children.Add(CreateHistoryRow(dist, minVolume, maxVolume));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载历史分布失败");
            }
        }
        
        /// <summary>
        /// 创建历史表头
        /// </summary>
        private Border CreateHistoryHeader(decimal threshold)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 3)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });  // 日期
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // <-x%
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // -x~0
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // 0~x
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // >x%
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // 成交额总额
            
            AddHistoryHeaderText(grid, "日期", 0);
            AddHistoryHeaderText(grid, $"<-{threshold}%", 1);
            AddHistoryHeaderText(grid, $"-{threshold}~0", 2);
            AddHistoryHeaderText(grid, $"0~{threshold}", 3);
            AddHistoryHeaderText(grid, $">{threshold}%", 4);
            AddHistoryHeaderText(grid, "成交额总额", 5);
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 添加历史表头文本
        /// </summary>
        private void AddHistoryHeaderText(Grid grid, string text, int column)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }
        
        /// <summary>
        /// 创建历史数据行
        /// </summary>
        private Border CreateHistoryRow(DailyMaDistribution dist, decimal minVolume, decimal maxVolume)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Colors.White)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            
            // 计算最大值用于色块深浅
            int maxCount = Math.Max(Math.Max(dist.BelowFarCount, dist.BelowNearCount),
                                   Math.Max(dist.AboveNearCount, dist.AboveFarCount));
            
            // 日期
            AddHistoryCellText(grid, dist.Date.ToString("MM-dd"), 0, Colors.Black, Colors.Transparent);
            
            // <-x%
            AddHistoryCellText(grid, dist.BelowFarCount.ToString(), 1, Colors.Black, 
                GetHeatmapColor(dist.BelowFarCount, maxCount));
            
            // -x~0
            AddHistoryCellText(grid, dist.BelowNearCount.ToString(), 2, Colors.Black, 
                GetHeatmapColor(dist.BelowNearCount, maxCount));
            
            // 0~x
            AddHistoryCellText(grid, dist.AboveNearCount.ToString(), 3, Colors.Black, 
                GetHeatmapColor(dist.AboveNearCount, maxCount));
            
            // >x%
            AddHistoryCellText(grid, dist.AboveFarCount.ToString(), 4, Colors.Black, 
                GetHeatmapColor(dist.AboveFarCount, maxCount));
            
            // 成交额总额（带蓝色热力图）
            var volumeText = dist.TotalQuoteVolume >= 1_000_000_000m
                ? $"${dist.TotalQuoteVolume / 1_000_000_000m:F2}B"
                : $"${dist.TotalQuoteVolume / 1_000_000m:F0}M";
            AddHistoryCellText(grid, volumeText, 5, Colors.Black, 
                GetVolumeHeatmapColor(dist.TotalQuoteVolume, minVolume, maxVolume));
            
            border.Child = grid;
            return border;
        }
        
        /// <summary>
        /// 获取热力图颜色（从白色到红色）
        /// </summary>
        private Color GetHeatmapColor(int value, int maxValue)
        {
            if (maxValue == 0) return Colors.White;
            
            // 计算比例（0-1）
            double ratio = (double)value / maxValue;
            
            // 从白色(255,255,255)渐变到红色(220,20,20)
            byte r = (byte)(255 - (35 * ratio));  // 255 -> 220
            byte g = (byte)(255 - (235 * ratio)); // 255 -> 20
            byte b = (byte)(255 - (235 * ratio)); // 255 -> 20
            
            return Color.FromRgb(r, g, b);
        }
        
        /// <summary>
        /// 获取成交额热力图颜色（从白色到蓝色）
        /// </summary>
        private Color GetVolumeHeatmapColor(decimal value, decimal minValue, decimal maxValue)
        {
            // 如果最大值和最小值相同，或者范围无效，返回白色
            if (maxValue <= minValue || maxValue == 0) return Colors.White;
            
            // 计算比例（0-1）
            double ratio = (double)((value - minValue) / (maxValue - minValue));
            
            // 确保比例在 0-1 范围内
            ratio = Math.Max(0, Math.Min(1, ratio));
            
            // 从白色(255,255,255)渐变到蓝色(20,100,220)
            byte r = (byte)(255 - (235 * ratio)); // 255 -> 20
            byte g = (byte)(255 - (155 * ratio)); // 255 -> 100
            byte b = (byte)(255 - (35 * ratio));  // 255 -> 220
            
            return Color.FromRgb(r, g, b);
        }
        
        /// <summary>
        /// 添加历史单元格文本
        /// </summary>
        private void AddHistoryCellText(Grid grid, string text, int column, Color textColor, Color backgroundColor)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(backgroundColor),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(2)
            };
            
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 11,
                Foreground = new SolidColorBrush(textColor),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeights.Normal
            };
            
            border.Child = textBlock;
            Grid.SetColumn(border, column);
            grid.Children.Add(border);
        }
        
        /// <summary>
        /// 重新计算历史数据按钮点击
        /// </summary>
        private async void BtnRecalculateHistory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 确认操作
                var result = MessageBox.Show(
                    "确定要重新计算所有历史数据吗？\n\n" +
                    "这将覆盖已有的历史数据（包括成交额等信息），\n" +
                    "重新计算过去30天的均线距离分布。\n\n" +
                    "此操作可能需要几分钟时间。",
                    "确认重新计算",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
                
                // 获取当前参数
                if (!int.TryParse(txtPeriod.Text, out int period) || period < 1 || period > 100)
                {
                    MessageBox.Show("请先设置有效的天数参数（1-100）", "参数错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!decimal.TryParse(txtThreshold.Text, out decimal threshold) || threshold < 1 || threshold > 50)
                {
                    MessageBox.Show("请先设置有效的距离百分比（1-50）", "参数错误", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 禁用按钮
                btnRecalculateHistory.IsEnabled = false;
                btnCalculate.IsEnabled = false;
                
                txtStatus.Text = "正在重新计算历史数据，请稍候...";
                _logger.LogInformation($"开始重新计算历史数据: period={period}, threshold={threshold}");
                
                // 强制重新计算所有历史数据
                await Task.Run(async () => await GenerateHistoricalDataAsync(period, threshold, forceRecalculate: true));
                
                // 重新加载历史分布
                await LoadHistoryDistributionsAsync(period, threshold);
                
                txtStatus.Text = "历史数据重新计算完成！";
                _logger.LogInformation("历史数据重新计算完成");
                
                MessageBox.Show(
                    "历史数据重新计算完成！\n\n已更新过去30天的所有数据。",
                    "完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重新计算历史数据失败");
                MessageBox.Show($"重新计算失败：{ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "重新计算失败";
            }
            finally
            {
                btnRecalculateHistory.IsEnabled = true;
                btnCalculate.IsEnabled = true;
            }
        }
        
        /// <summary>
        /// 生成过去30天的历史数据
        /// </summary>
        private async Task GenerateHistoricalDataAsync(int period, decimal threshold, bool forceRecalculate = false)
        {
            try
            {
                int calculatedCount = 0;
                int skippedCount = 0;
                
                // 检查过去30天，生成缺失的历史数据
                for (int daysAgo = 1; daysAgo <= 30; daysAgo++)
                {
                    var date = DateTime.Today.AddDays(-daysAgo);
                    
                    // 检查是否已有该日期的数据（如果不是强制重算）
                    if (!forceRecalculate && await _maDistanceService.HasDataForDateAsync(date, period, threshold))
                    {
                        skippedCount++;
                        continue; // 已有数据，跳过
                    }
                    
                    _logger.LogInformation($"{(forceRecalculate ? "重新计算" : "生成")}历史数据: {date:yyyy-MM-dd}");
                    
                    // 更新状态显示
                    await Dispatcher.InvokeAsync(() =>
                    {
                        txtStatus.Text = $"正在{(forceRecalculate ? "重新计算" : "生成")}历史数据... ({30 - daysAgo + 1}/30) {date:MM-dd}";
                    });
                    
                    // 计算该日期的数据
                    var historicalResult = await _maDistanceService.CalculateMaDistanceAsync(date, period, threshold);
                    
                    // 保存
                    await _maDistanceService.SaveAnalysisResultAsync(historicalResult);
                    calculatedCount++;
                }
                
                _logger.LogInformation($"历史数据处理完成: 计算={calculatedCount}, 跳过={skippedCount}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "生成历史数据时出错");
            }
        }
    }
} 