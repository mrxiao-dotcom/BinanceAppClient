using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BinanceApps.Core.Interfaces;

namespace BinanceApps.WPF
{
    /// <summary>
    /// RangeEditorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RangeEditorWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RangeEditorWindow>? _logger;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private static readonly string RangeDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BinanceApps",
            "Ranges"
        );

        public string? SavedRangeName { get; private set; }

        public RangeEditorWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger<RangeEditorWindow>>();
            _apiClient = _serviceProvider.GetRequiredService<IBinanceSimulatedApiClient>();
            
            // 确保目录存在
            if (!Directory.Exists(RangeDataPath))
            {
                Directory.CreateDirectory(RangeDataPath);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📁 创建范围数据目录: {RangeDataPath}");
            }
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 范围编辑器窗口已初始化");
        }

        /// <summary>
        /// 载入全部按钮点击事件
        /// </summary>
        private async void BtnLoadAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnLoadAll.IsEnabled = false;
                txtStatus.Text = "正在载入合约...";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🔄 开始载入全部合约...");

                // 获取所有交易对信息
                var allSymbols = await _apiClient.GetAllSymbolsInfoAsync();
                
                // 筛选 USDT 合约并排序
                var usdtSymbols = allSymbols
                    .Where(s => s.Symbol.EndsWith("USDT"))
                    .Select(s => s.Symbol)
                    .OrderBy(s => s)
                    .ToList();

                // 用逗号连接并显示
                txtSymbolList.Text = string.Join(",", usdtSymbols);
                
                txtStatus.Text = $"已载入 {usdtSymbols.Count} 个合约";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 已载入 {usdtSymbols.Count} 个合约");
                
                // 3秒后清除状态
                await Task.Delay(3000);
                txtStatus.Text = "";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "载入合约失败");
                txtStatus.Text = "载入失败";
                MessageBox.Show($"载入合约失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 载入合约失败: {ex.Message}");
            }
            finally
            {
                btnLoadAll.IsEnabled = true;
            }
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var rangeName = txtRangeName.Text?.Trim();
                if (string.IsNullOrEmpty(rangeName))
                {
                    MessageBox.Show("请输入范围名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var symbolText = txtSymbolList.Text?.Trim();
                if (string.IsNullOrEmpty(symbolText))
                {
                    MessageBox.Show("请输入合约列表", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 解析合约列表
                var symbols = symbolText.Split(new[] { ',', '，', ' ', '\n', '\r' }, 
                    StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();

                if (symbols.Count == 0)
                {
                    MessageBox.Show("合约列表为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 创建范围数据
                var rangeData = new RangeData
                {
                    Name = rangeName,
                    Symbols = symbols,
                    CreateTime = DateTime.Now,
                    UpdateTime = DateTime.Now
                };

                // 保存到文件
                var fileName = $"{SanitizeFileName(rangeName)}.json";
                var filePath = Path.Combine(RangeDataPath, fileName);
                
                var json = JsonSerializer.Serialize(rangeData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                File.WriteAllText(filePath, json);

                SavedRangeName = rangeName;
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ 范围已保存: {rangeName} ({symbols.Count} 个合约) -> {filePath}");
                MessageBox.Show($"范围 \"{rangeName}\" 已保存！\n合约数量: {symbols.Count}", "成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存范围失败");
                MessageBox.Show($"保存失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 保存范围失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// 获取所有已保存的范围
        /// </summary>
        public static List<RangeData> LoadAllRanges()
        {
            var ranges = new List<RangeData>();
            
            try
            {
                if (!Directory.Exists(RangeDataPath))
                {
                    return ranges;
                }

                var files = Directory.GetFiles(RangeDataPath, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var range = JsonSerializer.Deserialize<RangeData>(json);
                        if (range != null)
                        {
                            ranges.Add(range);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ⚠️ 加载范围文件失败: {file}, {ex.Message}");
                    }
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📋 已加载 {ranges.Count} 个范围");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 加载范围列表失败: {ex.Message}");
            }

            return ranges.OrderBy(r => r.Name).ToList();
        }

        /// <summary>
        /// 删除指定范围
        /// </summary>
        public static bool DeleteRange(string rangeName)
        {
            try
            {
                var fileName = $"{SanitizeFileName(rangeName)}.json";
                var filePath = Path.Combine(RangeDataPath, fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 🗑️ 已删除范围: {rangeName}");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ 删除范围失败: {rangeName}, {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 范围数据模型
    /// </summary>
    public class RangeData
    {
        public string Name { get; set; } = "";
        public List<string> Symbols { get; set; } = new();
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}

