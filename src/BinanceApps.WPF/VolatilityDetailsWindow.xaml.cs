using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BinanceApps.WPF
{
    public partial class VolatilityDetailsWindow : Window
    {
        private List<VolatilityDisplayItem> _volatilityItems;
        private System.Windows.Threading.DispatcherTimer? _titleResetTimer;

        public VolatilityDetailsWindow(DateTime date, List<SymbolVolatility> topSymbols)
        {
            InitializeComponent();
            LoadData(date, topSymbols);
            
            // 注册窗口关闭事件
            this.Closing += VolatilityDetailsWindow_Closing;
        }

        /// <summary>
        /// 加载波动率数据
        /// </summary>
        private void LoadData(DateTime date, List<SymbolVolatility> topSymbols)
        {
            try
            {
                // 设置标题和日期
                txtDate.Text = $"{date:yyyy年MM月dd日}";
                
                // 创建显示数据
                _volatilityItems = topSymbols.Take(30).Select((item, index) => new VolatilityDisplayItem
                {
                    Rank = index + 1,
                    Symbol = item.Symbol,
                    DisplayName = GetDisplayName(item.Symbol),
                    Volatility = item.Volatility,
                    VolatilityText = $"{(item.Volatility * 100):F2}%", // 修正：乘以100显示百分比
                    VolatilityColor = GetVolatilityColorBrush(item.Volatility * 100), // 修正：传入百分比值
                    PriceRange = $"高: {item.HighPrice:F4} 低: {item.LowPrice:F4}",
                    HighPrice = item.HighPrice,
                    LowPrice = item.LowPrice,
                    PriceChangePercent = item.PriceChangePercent,
                    PriceChangeText = $"{item.PriceChangePercent:+0.00;-0.00;0.00}%",
                    PriceChangeColor = item.PriceChangePercent >= 0 ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                    QuoteVolume = item.QuoteVolume,
                    QuoteVolumeText = FormatQuoteVolume(item.QuoteVolume)
                }).ToList();

                // 绑定数据到ListView
                lvSymbols.ItemsSource = _volatilityItems;

                // 设置统计信息
                var avgVolatility = topSymbols.Take(30).Average(x => x.Volatility) * 100;
                var maxVolatility = topSymbols.Take(30).Max(x => x.Volatility) * 100;
                var minVolatility = topSymbols.Take(30).Min(x => x.Volatility) * 100;
                
                txtStats.Text = $"平均波动率: {avgVolatility:F2}%  |  最高: {maxVolatility:F2}%  |  最低: {minVolatility:F2}%";

                Console.WriteLine($"📊 波动率详情窗口加载完成，显示 {_volatilityItems.Count} 个币种");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载波动率数据失败: {ex.Message}");
                MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取币种显示名称
        /// </summary>
        private string GetDisplayName(string symbol)
        {
            if (symbol.EndsWith("USDT"))
            {
                var baseCurrency = symbol.Substring(0, symbol.Length - 4);
                return $"{baseCurrency}/USDT";
            }
            return symbol;
        }

        /// <summary>
        /// 格式化成交额显示
        /// </summary>
        private string FormatQuoteVolume(decimal volume)
        {
            if (volume >= 100000000) // 大于1亿
            {
                return $"{(volume / 100000000):F1}亿";
            }
            else if (volume >= 10000) // 大于1万
            {
                return $"{(volume / 10000):F1}万";
            }
            else
            {
                return $"{volume:F0}";
            }
        }

        /// <summary>
        /// 根据波动率获取颜色画刷
        /// </summary>
        private Brush GetVolatilityColorBrush(decimal volatility)
        {
            // 根据波动率返回不同的颜色
            if (volatility >= 15)
                return new SolidColorBrush(Color.FromRgb(220, 38, 127)); // 深红色 - 极高波动
            else if (volatility >= 10)
                return new SolidColorBrush(Color.FromRgb(239, 68, 68)); // 红色 - 高波动
            else if (volatility >= 7)
                return new SolidColorBrush(Color.FromRgb(245, 101, 101)); // 橙红色 - 中高波动
            else if (volatility >= 5)
                return new SolidColorBrush(Color.FromRgb(251, 146, 60)); // 橙色 - 中等波动
            else if (volatility >= 3)
                return new SolidColorBrush(Color.FromRgb(252, 211, 77)); // 黄色 - 中低波动
            else
                return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // 绿色 - 低波动
        }

        /// <summary>
        /// ListView点击事件 - 复制币种名称到剪贴板
        /// </summary>
        private void LvSymbols_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (lvSymbols.SelectedItem is VolatilityDisplayItem selectedItem)
                {
                    // 复制到剪贴板
                    Clipboard.SetText(selectedItem.Symbol);
                    
                    // 显示复制成功的提示
                    var originalTitle = txtTitle.Text;
                    txtTitle.Text = $"✅ 已复制: {selectedItem.Symbol}";
                    txtTitle.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    
                    // 2秒后恢复原标题
                    _titleResetTimer?.Stop(); // 停止之前的计时器
                    _titleResetTimer = new System.Windows.Threading.DispatcherTimer();
                    _titleResetTimer.Interval = TimeSpan.FromSeconds(2);
                    _titleResetTimer.Tick += (s, args) =>
                    {
                        txtTitle.Text = originalTitle;
                        txtTitle.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80));
                        _titleResetTimer?.Stop();
                    };
                    _titleResetTimer.Start();

                    Console.WriteLine($"📋 已复制币种名称到剪贴板: {selectedItem.Symbol}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 复制到剪贴板失败: {ex.Message}");
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void VolatilityDetailsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 停止并清理计时器
                _titleResetTimer?.Stop();
                _titleResetTimer = null;
                
                Console.WriteLine("📊 波动率详情窗口正在关闭，已清理资源");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清理波动率详情窗口资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// 波动率显示项数据模型
    /// </summary>
    public class VolatilityDisplayItem
    {
        public int Rank { get; set; }
        public string Symbol { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public decimal Volatility { get; set; }
        public string VolatilityText { get; set; } = "";
        public Brush VolatilityColor { get; set; } = Brushes.Black;
        public string PriceRange { get; set; } = "";
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal PriceChangePercent { get; set; } // 24H涨幅
        public string PriceChangeText { get; set; } = ""; // 24H涨幅显示文本
        public Brush PriceChangeColor { get; set; } = Brushes.Black; // 24H涨幅颜色
        public decimal QuoteVolume { get; set; } // 24H成交额
        public string QuoteVolumeText { get; set; } = ""; // 24H成交额显示文本
    }
} 