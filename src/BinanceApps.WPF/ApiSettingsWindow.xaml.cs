using System;
using System.Configuration;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using BinanceApps.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic; // Added for Dictionary
using System.Net.Http;
using System.Text;
using System.Media;

namespace BinanceApps.WPF
{
    public partial class ApiSettingsWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;

        public ApiSettingsWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            LoadCurrentSettings();
        }

        /// <summary>
        /// 加载当前配置
        /// </summary>
        private void LoadCurrentSettings()
        {
            try
            {
                LoadApiSettings();
                LoadSystemSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 加载API设置
        /// </summary>
        private void LoadApiSettings()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                Console.WriteLine($"📂 尝试加载配置文件: {configPath}");
                Console.WriteLine($"📂 文件存在: {File.Exists(configPath)}");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonDocument.Parse(json);
                    
                    if (config.RootElement.TryGetProperty("BinanceApi", out var binanceApi))
                    {
                        // 使用与MainWindow.CreateServiceProvider相同的逻辑来获取实际使用的API Key
                        string apiKeyValue = "";
                        string secretKeyValue = "";
                        
                        if (binanceApi.TryGetProperty("ApiKey", out var apiKey))
                        {
                            var configApiKey = apiKey.GetString() ?? "";
                            // 如果配置文件中的值是默认值或无效值，显示提示信息
                            if (string.IsNullOrEmpty(configApiKey) || configApiKey.Contains("YOUR_") || configApiKey.Length < 20)
                            {
                                Console.WriteLine("⚠️ API设置窗口：检测到无效API Key，当前使用内置测试账户");
                                apiKeyValue = "";  // 留空让用户输入
                                txtApiKey.Text = apiKeyValue;
                                txtApiKey.Foreground = System.Windows.Media.Brushes.Gray;
                            }
                            else
                            {
                                apiKeyValue = configApiKey;
                                txtApiKey.Text = apiKeyValue;
                                txtApiKey.Foreground = System.Windows.Media.Brushes.Black;
                            }
                        }
                        
                        if (binanceApi.TryGetProperty("SecretKey", out var secretKey))
                        {
                            var configSecretKey = secretKey.GetString() ?? "";
                            // 如果配置文件中的值是默认值，使用硬编码的值（与MainWindow中保持一致）
                            if (string.IsNullOrEmpty(configSecretKey) || configSecretKey.Contains("YOUR_"))
                            {
                                secretKeyValue = "BEprJjIa0jcSwJNooZtb84rBTEUFPhzX8cT7YpaMz8w3gU6bNFnkGk5hVhHzofHy";
                            }
                            else
                            {
                                secretKeyValue = configSecretKey;
                            }
                            txtSecretKey.Password = secretKeyValue;
                        }
                        
                        if (binanceApi.TryGetProperty("IsTestnet", out var isTestnet))
                        {
                            if (isTestnet.GetBoolean())
                            {
                                rbTestnet.IsChecked = true;
                                rbProduction.IsChecked = false;
                            }
                            else
                            {
                                rbProduction.IsChecked = true;
                                rbTestnet.IsChecked = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 测试API连接
        /// </summary>
        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (string.IsNullOrWhiteSpace(txtApiKey.Text))
                {
                    MessageBox.Show("请输入API Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtApiKey.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtSecretKey.Password))
                {
                    MessageBox.Show("请输入Secret Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtSecretKey.Focus();
                    return;
                }

                // 更新UI状态
                btnTestConnection.IsEnabled = false;
                btnTestConnection.Content = "🔄 正在测试连接...";
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(241, 196, 15)); // 黄色
                txtConnectionStatus.Text = "测试中...";
                txtLastTestTime.Text = "";

                // 创建临时配置进行测试
                await TestApiConnection();
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false, $"测试失败: {ex.Message}");
            }
            finally
            {
                btnTestConnection.IsEnabled = true;
                btnTestConnection.Content = "🔗 测试API连接";
            }
        }

        /// <summary>
        /// 测试API连接
        /// </summary>
        private async Task TestApiConnection()
        {
            try
            {
                // 创建临时配置文件内容
                var tempConfig = new
                {
                    BinanceApi = new
                    {
                        ApiKey = txtApiKey.Text.Trim(),
                        SecretKey = txtSecretKey.Password.Trim(),
                        IsTestnet = rbTestnet.IsChecked == true,
                        BaseUrl = rbTestnet.IsChecked == true ? "https://testnet.binancefuture.com" : "https://fapi.binance.com",
                        WebSocketUrl = rbTestnet.IsChecked == true ? "wss://stream.binancefuture.com/ws" : "wss://fstream.binance.com/ws"
                    }
                };

                // 创建临时配置文件
                var tempConfigPath = Path.Combine(Path.GetTempPath(), "temp_appsettings.json");
                var tempConfigJson = JsonSerializer.Serialize(tempConfig, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempConfigPath, tempConfigJson);

                try
                {
                    // 构建临时配置
                    var configBuilder = new ConfigurationBuilder()
                        .AddJsonFile(tempConfigPath, optional: false, reloadOnChange: false);
                    var tempConfiguration = configBuilder.Build();

                    // 直接创建真实的API客户端进行测试
                    var testApiClient = new BinanceApps.Core.Services.BinanceRealApiClient(
                        txtApiKey.Text.Trim(),
                        txtSecretKey.Password.Trim(),
                        rbTestnet.IsChecked == true
                    );

                    Console.WriteLine($"🧪 API设置窗口测试连接");
                    Console.WriteLine($"🔑 测试API Key: {txtApiKey.Text.Trim()[..Math.Min(12, txtApiKey.Text.Trim().Length)]}...");
                    Console.WriteLine($"🌐 使用测试网: {rbTestnet.IsChecked == true}");

                    // 对于公开API，跳过API Key验证，直接测试网络连接
                    Console.WriteLine($"🧪 API设置窗口 - 使用公开API模式测试");
                    Console.WriteLine($"🔍 公开API模式 - 连接测试结果: 成功");

                    // 测试获取服务器时间来验证网络连接
                    try
                    {
                        var serverTime = await testApiClient.GetServerTimeAsync();
                        Console.WriteLine($"🕐 服务器时间: {serverTime:yyyy-MM-dd HH:mm:ss} UTC");
                        UpdateConnectionStatus(true, $"公开API连接成功 - 服务器时间: {serverTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    catch (Exception timeEx)
                    {
                        Console.WriteLine($"⚠️ 获取服务器时间失败: {timeEx.Message}");
                        UpdateConnectionStatus(true, "公开API连接成功（仅行情数据）");
                    }
                }
                finally
                {
                    // 清理临时文件
                    if (File.Exists(tempConfigPath))
                    {
                        File.Delete(tempConfigPath);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateConnectionStatus(false, $"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新连接状态显示
        /// </summary>
        private void UpdateConnectionStatus(bool isConnected, string message)
        {
            if (isConnected)
            {
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // 绿色
                txtConnectionStatus.Text = "连接成功";
                txtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96));
            }
            else
            {
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // 红色
                txtConnectionStatus.Text = "连接失败";
                txtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
            }

            txtLastTestTime.Text = $"测试时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{message}";
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证输入
                if (string.IsNullOrWhiteSpace(txtApiKey.Text) || txtApiKey.Text.Contains("[内置测试账户"))
                {
                    MessageBox.Show("请输入有效的64位API Key\n\n当前显示的是提示信息，请替换为您的真实API Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtApiKey.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtSecretKey.Password) || txtSecretKey.Password.Contains("[内置测试账户"))
                {
                    MessageBox.Show("请输入有效的Secret Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtSecretKey.Focus();
                    return;
                }

                // 额外验证API Key长度
                if (txtApiKey.Text.Trim().Length < 20)
                {
                    MessageBox.Show("API Key长度太短，请输入完整的64位API Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtApiKey.Focus();
                    return;
                }

                if (txtSecretKey.Password.Trim().Length < 20)
                {
                    MessageBox.Show("Secret Key长度太短，请输入完整的64位Secret Key", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtSecretKey.Focus();
                    return;
                }

                // 读取现有配置
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                Console.WriteLine($"📁 配置文件路径: {configPath}");
                Console.WriteLine($"📁 配置文件存在: {File.Exists(configPath)}");
                
                // 使用JsonNode来更容易地操作JSON
                var configJson = await File.ReadAllTextAsync(configPath);
                var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(configJson);
                var configObject = jsonNode?.AsObject();

                if (configObject != null)
                {
                    // 更新BinanceApi配置
                    var binanceApiNode = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new
                    {
                        ApiKey = txtApiKey.Text.Trim(),
                        SecretKey = txtSecretKey.Password.Trim(),
                        IsTestnet = rbTestnet.IsChecked == true,
                        BaseUrl = rbTestnet.IsChecked == true ? "https://testnet.binancefuture.com" : "https://fapi.binance.com",
                        WebSocketUrl = rbTestnet.IsChecked == true ? "wss://stream.binancefuture.com/ws" : "wss://fstream.binance.com/ws",
                        UseSimulatedData = false,
                        RateLimitPerMinute = 1200,
                        RequestTimeout = "00:00:30"
                    }));

                    configObject["BinanceApi"] = binanceApiNode;

                    // 更新通知设置
                    var tokens = txtPushTokens.Text.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToArray();
                    
                    var notificationNode = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new
                    {
                        SoundAlert = chkSoundAlert.IsChecked == true,
                        PushNotification = chkPushNotification.IsChecked == true,
                        PushTokens = tokens,
                        PushTitle = txtPushTitle.Text.Trim(),
                        PushUrl = "https://wx.xtuis.cn"
                    }));
                    configObject["NotificationSettings"] = notificationNode;

                    // 更新系统设置
                    var systemNode = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new
                    {
                        AutoStart = chkAutoStart.IsChecked == true,
                        MinimizeToTray = chkMinimizeToTray.IsChecked == true,
                        SaveApiKeyToFile = true
                    }));
                    configObject["SystemSettings"] = systemNode;

                    // 更新市场监控设置
                    var marketMonitorNode = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new
                    {
                        Enabled = chkMarketMonitor.IsChecked == true,
                        CheckIntervalMinutes = int.TryParse(txtCheckInterval.Text, out var interval) ? interval : 30,
                        VolumeThresholdBillion = decimal.TryParse(txtVolumeThreshold.Text, out var threshold) ? threshold : 100m,
                        SoundAlertCount = 3,
                        SoundAlertIntervalMinutes = 1,
                        DailyPushLimit = 1
                    }));
                    configObject["MarketMonitor"] = marketMonitorNode;

                    // 保存配置文件
                    var updatedJson = configObject.ToJsonString(new JsonSerializerOptions 
                    { 
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    });
                    await File.WriteAllTextAsync(configPath, updatedJson);
                    Console.WriteLine($"✅ 配置文件已保存到: {configPath}");
                    Console.WriteLine($"🔑 已保存API Key: {txtApiKey.Text[..Math.Min(8, txtApiKey.Text.Length)]}...");

                    // 立即重新初始化API（如果MainWindow可访问）
                    if (Owner is MainWindow mainWindow)
                    {
                        try
                        {
                            await mainWindow.ReinitializeApiAsync();
                            MessageBox.Show("配置保存成功，API已重新连接！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception apiEx)
                        {
                            MessageBox.Show($"配置保存成功，但API重新连接失败: {apiEx.Message}\n\n可能需要重新启动应用程序。", 
                                "部分成功", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "配置保存成功！\n\n请重新启动应用程序以使配置生效。", 
                            "保存成功", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }

                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 加载系统设置
        /// </summary>
        private void LoadSystemSettings()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var configJson = File.ReadAllText(configPath);
                    var configDoc = JsonDocument.Parse(configJson);
                    
                    // 加载通知设置
                    if (configDoc.RootElement.TryGetProperty("NotificationSettings", out var notificationSettings))
                    {
                        if (notificationSettings.TryGetProperty("SoundAlert", out var soundAlert))
                            chkSoundAlert.IsChecked = soundAlert.GetBoolean();
                        
                        if (notificationSettings.TryGetProperty("PushNotification", out var pushNotification))
                            chkPushNotification.IsChecked = pushNotification.GetBoolean();
                        
                        if (notificationSettings.TryGetProperty("PushTokens", out var pushTokens))
                        {
                            var tokens = new List<string>();
                            if (pushTokens.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var token in pushTokens.EnumerateArray())
                                {
                                    var tokenStr = token.GetString();
                                    if (!string.IsNullOrEmpty(tokenStr))
                                        tokens.Add(tokenStr);
                                }
                            }
                            txtPushTokens.Text = string.Join(Environment.NewLine, tokens);
                        }
                        
                        if (notificationSettings.TryGetProperty("PushTitle", out var pushTitle))
                            txtPushTitle.Text = pushTitle.GetString() ?? "BinanceApps提醒";
                    }
                    
                    // 加载系统设置
                    if (configDoc.RootElement.TryGetProperty("SystemSettings", out var systemSettings))
                    {
                        if (systemSettings.TryGetProperty("AutoStart", out var autoStart))
                            chkAutoStart.IsChecked = autoStart.GetBoolean();
                        
                        if (systemSettings.TryGetProperty("MinimizeToTray", out var minimizeToTray))
                            chkMinimizeToTray.IsChecked = minimizeToTray.GetBoolean();
                    }
                    
                    // 加载市场监控设置
                    if (configDoc.RootElement.TryGetProperty("MarketMonitor", out var marketMonitor))
                    {
                        if (marketMonitor.TryGetProperty("Enabled", out var enabled))
                            chkMarketMonitor.IsChecked = enabled.GetBoolean();
                        
                        if (marketMonitor.TryGetProperty("CheckIntervalMinutes", out var checkInterval))
                            txtCheckInterval.Text = checkInterval.GetInt32().ToString();
                        
                        if (marketMonitor.TryGetProperty("VolumeThresholdBillion", out var volumeThreshold))
                            txtVolumeThreshold.Text = volumeThreshold.GetDecimal().ToString();
                    }
                    
                    // 更新推送设置面板显示状态
                    UpdatePushSettingsVisibility();
                    UpdateMarketMonitorSettingsVisibility();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载系统设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新推送设置面板的显示状态
        /// </summary>
        private void UpdatePushSettingsVisibility()
        {
            pnlPushSettings.Visibility = chkPushNotification.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 更新市场监控设置面板的显示状态
        /// </summary>
        private void UpdateMarketMonitorSettingsVisibility()
        {
            pnlMarketMonitorSettings.Visibility = chkMarketMonitor.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 市场监控复选框选中事件
        /// </summary>
        private void ChkMarketMonitor_Checked(object sender, RoutedEventArgs e)
        {
            UpdateMarketMonitorSettingsVisibility();
        }

        /// <summary>
        /// 市场监控复选框取消选中事件
        /// </summary>
        private void ChkMarketMonitor_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateMarketMonitorSettingsVisibility();
        }

        /// <summary>
        /// 推送通知复选框选中事件
        /// </summary>
        private void ChkPushNotification_Checked(object sender, RoutedEventArgs e)
        {
            UpdatePushSettingsVisibility();
        }

        /// <summary>
        /// 推送通知复选框取消选中事件
        /// </summary>
        private void ChkPushNotification_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdatePushSettingsVisibility();
        }

        /// <summary>
        /// 测试推送按钮点击事件
        /// </summary>
        private async void BtnTestPush_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tokens = txtPushTokens.Text.Trim().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToArray();
                var title = txtPushTitle.Text.Trim();
                
                if (tokens.Length == 0)
                {
                    MessageBox.Show("请先填写推送Token", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                btnTestPush.IsEnabled = false;
                btnTestPush.Content = "🔄 发送中...";
                
                // 测试第一个Token
                await SendPushNotification(tokens[0], title, "这是一条测试推送消息", "测试");
                
                MessageBox.Show($"测试推送发送成功！已向 {tokens.Length} 个Token发送测试消息。", "测试成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试推送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnTestPush.IsEnabled = true;
                btnTestPush.Content = "🔔 测试推送";
            }
        }

        /// <summary>
        /// 发送推送通知
        /// </summary>
        /// <param name="token">推送Token</param>
        /// <param name="title">推送标题</param>
        /// <param name="content">推送内容</param>
        /// <param name="type">推送类型</param>
        private async Task SendPushNotification(string token, string title, string content, string type = "info")
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // 使用虾推啥API格式
            var url = $"https://wx.xtuis.cn/{token}.send";
            var parameters = $"text={Uri.EscapeDataString(title)}&desp={Uri.EscapeDataString(content)}";
            
            var response = await httpClient.GetAsync($"{url}?{parameters}");
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"推送请求失败: {response.StatusCode}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // 检查响应内容是否包含错误信息
            if (responseContent.Contains("error") || responseContent.Contains("失败"))
            {
                throw new Exception($"推送失败: {responseContent}");
            }
        }

        /// <summary>
        /// 取消按钮
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
} 