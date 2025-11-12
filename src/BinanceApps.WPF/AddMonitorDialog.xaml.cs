using System;
using System.Windows;
using BinanceApps.Core.Models;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 添加监控对话框
    /// </summary>
    public partial class AddMonitorDialog : Window
    {
        private readonly IBinanceSimulatedApiClient? _apiClient;
        
        public string Symbol { get; private set; } = string.Empty;
        public decimal EntryPrice { get; private set; }
        public MonitorType MonitorType { get; private set; }

        public AddMonitorDialog(IServiceProvider? serviceProvider = null)
        {
            InitializeComponent();
            _apiClient = serviceProvider?.GetService<IBinanceSimulatedApiClient>();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(txtSymbol.Text))
            {
                MessageBox.Show("请输入合约名称", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtEntryPrice.Text, out var price) || price <= 0)
            {
                MessageBox.Show("请输入有效的价格", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Symbol = txtSymbol.Text.ToUpper().Trim();
            EntryPrice = price;
            MonitorType = rbLong.IsChecked == true ? MonitorType.Long : MonitorType.Short;

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 获取价格按钮点击
        /// </summary>
        private async void BtnGetPrice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtSymbol.Text))
                {
                    MessageBox.Show("请先输入合约名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtSymbol.Focus();
                    return;
                }

                var symbol = txtSymbol.Text.ToUpper().Trim();
                btnGetPrice.IsEnabled = false;
                btnGetPrice.Content = "获取中...";

                if (_apiClient != null)
                {
                    // 从API获取最新价格
                    var ticker = await _apiClient.Get24hrPriceStatisticsAsync(symbol);
                    if (ticker != null && ticker.LastPrice > 0)
                    {
                        txtEntryPrice.Text = ticker.LastPrice.ToString("F8");
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 获取 {symbol} 价格成功: {ticker.LastPrice:F8}");
                    }
                    else
                    {
                        MessageBox.Show($"无法获取 {symbol} 的价格，请检查合约名称是否正确", "获取失败", 
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("API客户端未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 获取价格失败: {ex.Message}");
                MessageBox.Show($"获取价格失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnGetPrice.IsEnabled = true;
                btnGetPrice.Content = "获取价格";
            }
        }
    }
}

