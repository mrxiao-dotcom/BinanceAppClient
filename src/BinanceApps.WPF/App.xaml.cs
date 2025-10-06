using System;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Configuration;
using System.Windows.Media;
using RegisterSrv.ClientSDK;
using RegisterSrv.AutoUpdate;

namespace BinanceApps.WPF
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        public static FixedUpdateManager? UpdateManager { get; private set; }
        
        protected override void OnStartup(StartupEventArgs e)
        {
            Console.WriteLine("🚀 BinanceApps 启动");
            
            // 设置全局异常处理
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            
            // 使用智能更新后，配置文件不会被覆盖，无需恢复
            // RestoreConfigBackupIfExists(); // 已不再需要
            
            // 从配置文件读取应用信息
            string appId = ConfigurationManager.AppSettings["ApplicationId"] ?? "BinanceApps2024";
            string appName = ConfigurationManager.AppSettings["ApplicationName"] ?? "BinanceApps";
            string serverUrl = ConfigurationManager.AppSettings["LicenseServerUrl"] ?? "http://localhost:5232";
            
            Console.WriteLine($"📋 应用信息: {appName} (ID: {appId})");
            Console.WriteLine($"🌐 服务器地址: {serverUrl}");
            
            // 从 AppData 加载注册码到内存（不修改 App.config 文件）
            LoadLicenseKeyFromAppData();
            
            // 强制使用App.config中的服务器地址初始化LicenseManager
            LicenseManager.Initialize(appId, serverUrl);
            Console.WriteLine("✅ 许可证管理器已初始化");
            
            // 初始化自动更新管理器（从App.config读取服务器地址）
            var updateConfig = new UpdateConfig
            {
                ServerUrl = serverUrl,  // ✅ 从 App.config 动态读取服务器地址
                AppId = appId,
                AppName = appName,
                CurrentVersion = GetApplicationVersion(),
                AutoCheckOnStartup = false,  // 关闭启动时自动检查，只在用户手动点击时检查
                SilentUpdate = false
            };
            
            // 调试输出
            Console.WriteLine($"🔧 [调试] 更新服务器 URL: {updateConfig.ServerUrl}");
            Console.WriteLine($"🔧 [调试] 应用 ID: {updateConfig.AppId}");
            Console.WriteLine($"🔧 [调试] 应用名称: {updateConfig.AppName}");
            Console.WriteLine($"🔧 [调试] 当前版本: {updateConfig.CurrentVersion}");
            
            UpdateManager = new FixedUpdateManager(updateConfig);
            Console.WriteLine($"✅ 自动更新管理器已初始化 (版本: {updateConfig.CurrentVersion})");
            
            // 先调用base.OnStartup确保XAML资源完全加载
            base.OnStartup(e);
            
            // 使用后台API进行许可证验证，避免UI组件的XAML问题
            Console.WriteLine("🔐 开始后台许可证验证...");
            
            Task.Run(async () =>
            {
                try
                {
                    // 先验证当前许可证
                    var result = await LicenseManager.ValidateCurrentLicenseAsync();
                    Console.WriteLine($"🔍 许可证验证结果: IsValid={result.IsValid}, Message={result.Message}");
                    
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (result.IsValid || result.Message.Contains("验证成功"))
                        {
                            Console.WriteLine("✅ 许可证验证成功，启动主窗口");
                            
                            // 先显示主窗口
                            var mainWindow = new MainWindow();
                            MainWindow = mainWindow;
                            mainWindow.Show();
                            Console.WriteLine("✅ 主窗口已启动");
                            
                            // 已关闭启动时自动检查更新
                            // 用户可以通过"帮助 → 检查更新"菜单手动检查
                            Console.WriteLine("ℹ️  启动时自动检查更新已关闭，请使用菜单手动检查");
                        }
                        else
                        {
                            Console.WriteLine("❌ 许可证验证失败，显示验证配置界面");
                            ShowLicenseValidationDialog(appName);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 许可证验证异常: {ex.Message}");
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"许可证验证失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                    });
                }
            });
        }

        private string GetApplicationVersion()
        {
            // 优先读取保存的版本号（更新后从服务器获取的版本）
            var savedVersion = GetSavedVersionFromConfig();
            if (!string.IsNullOrEmpty(savedVersion))
            {
                Console.WriteLine($"📌 使用已保存的版本号: {savedVersion}（来自最后一次更新）");
                return savedVersion;
            }
            
            // 如果没有保存的版本号，则从程序集读取
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = $"{version?.Major}.{version?.Minor}.{version?.Build}";
            Console.WriteLine($"📌 使用程序集版本号: {versionString}");
            return versionString;
        }
        
        /// <summary>
        /// 从配置文件读取保存的版本号
        /// </summary>
        private string GetSavedVersionFromConfig()
        {
            try
            {
                return ConfigurationManager.AppSettings["CurrentAppVersion"] ?? "";
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// 保存版本号到配置文件
        /// </summary>
        public static void SaveCurrentVersion(string version)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                
                if (configFile.AppSettings.Settings["CurrentAppVersion"] != null)
                {
                    configFile.AppSettings.Settings["CurrentAppVersion"].Value = version;
                }
                else
                {
                    configFile.AppSettings.Settings.Add("CurrentAppVersion", version);
                }
                
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                
                Console.WriteLine($"💾 已保存当前版本号: {version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 保存版本号失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检查并恢复配置文件备份（更新后保护用户注册码）
        /// </summary>
        private void RestoreConfigBackupIfExists()
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var configFile = System.IO.Path.Combine(appDir, "App.config");
                
                // 查找最新的备份文件
                var backupFiles = System.IO.Directory.GetFiles(appDir, "App.config.backup_*")
                    .OrderByDescending(f => new System.IO.FileInfo(f).CreationTime)
                    .ToList();
                
                if (backupFiles.Count > 0)
                {
                    var latestBackup = backupFiles[0];
                    Console.WriteLine($"🔄 发现配置备份文件");
                    Console.WriteLine($"   备份文件: {System.IO.Path.GetFileName(latestBackup)}");
                    
                    // 读取备份文件内容
                    var backupContent = System.IO.File.ReadAllText(latestBackup);
                    
                    // 检查备份文件是否有注册码（更精确的检查）
                    var hasLicenseKey = false;
                    var licenseKeyValue = "";
                    
                    // 使用正则表达式提取 LicenseKey 的值
                    var match = System.Text.RegularExpressions.Regex.Match(
                        backupContent, 
                        @"<add\s+key\s*=\s*""LicenseKey""\s+value\s*=\s*""([^""]*)"""
                    );
                    
                    if (match.Success)
                    {
                        licenseKeyValue = match.Groups[1].Value;
                        hasLicenseKey = !string.IsNullOrEmpty(licenseKeyValue);
                        Console.WriteLine($"   🔑 备份中的 LicenseKey: {(hasLicenseKey ? licenseKeyValue : "（空）")}");
                    }
                    else
                    {
                        Console.WriteLine($"   ⚠️ 备份文件中未找到 LicenseKey 配置");
                    }
                    
                    // 无论是否有注册码，都恢复备份（因为备份包含其他重要配置）
                    Console.WriteLine($"   📋 恢复配置文件（保护用户所有配置）...");
                    System.IO.File.Copy(latestBackup, configFile, true);
                    Console.WriteLine($"   ✅ 已恢复配置文件");
                    
                    if (hasLicenseKey)
                    {
                        Console.WriteLine($"   ✅ 注册码已保留: {licenseKeyValue.Substring(0, Math.Min(10, licenseKeyValue.Length))}...");
                    }
                    
                    // 重新加载配置
                    try
                    {
                        ConfigurationManager.RefreshSection("appSettings");
                        Console.WriteLine($"   ✅ 配置已重新加载");
                    }
                    catch (Exception refreshEx)
                    {
                        Console.WriteLine($"   ⚠️ 配置重新加载失败: {refreshEx.Message}");
                    }
                    
                    // 删除备份文件
                    foreach (var backup in backupFiles)
                    {
                        try { System.IO.File.Delete(backup); } catch { }
                    }
                    Console.WriteLine($"   ✅ 已清理 {backupFiles.Count} 个备份文件");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 恢复配置文件失败: {ex.Message}");
            }
        }

        private void ShowLicenseValidationDialog(string appName)
        {
            // 创建完整的许可证验证和配置窗口
            var validationWindow = new Window()
            {
                Title = "BinanceApps - 许可证验证",
                Width = 650,
                Height = 680,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 600,
                MinHeight = 650
            };
            
            var scrollViewer = new System.Windows.Controls.ScrollViewer()
            {
                VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled
            };
            
            var mainPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(25) };
            
            // 标题
            var titleBlock = new System.Windows.Controls.TextBlock()
            {
                Text = "BinanceApps 许可证验证",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            mainPanel.Children.Add(titleBlock);
            
            // 服务器配置区域
            var serverGroup = new System.Windows.Controls.GroupBox()
            {
                Header = "服务器配置",
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(5)
            };
            var serverPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(10) };
            
            serverPanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "许可证服务器：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var serverAddressBox = new System.Windows.Controls.TextBox() 
            { 
                Height = 25, 
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10),
                Text = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"] ?? ""
            };
            serverPanel.Children.Add(serverAddressBox);
            
            var testServerButton = new System.Windows.Controls.Button()
            {
                Content = "测试服务器连接",
                Width = 120,
                Height = 30,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            serverPanel.Children.Add(testServerButton);
            
            var serverStatusText = new System.Windows.Controls.TextBlock()
            {
                Text = "",
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            serverPanel.Children.Add(serverStatusText);
            
            serverGroup.Content = serverPanel;
            mainPanel.Children.Add(serverGroup);
            
            // 机器码区域
            var machineGroup = new System.Windows.Controls.GroupBox()
            {
                Header = "机器码信息",
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(5)
            };
            var machinePanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(10) };
            
            machinePanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "您的机器码（请提供给许可证提供商）：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var machineCode = LicenseManager.GetMachineCode();
            var machineCodeBox = new System.Windows.Controls.TextBox()
            {
                Text = machineCode,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Height = 25,
                Margin = new Thickness(0, 0, 0, 10)
            };
            machinePanel.Children.Add(machineCodeBox);
            
            var copyMachineCodeButton = new System.Windows.Controls.Button()
            {
                Content = "复制机器码",
                Width = 100,
                Height = 30,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            machinePanel.Children.Add(copyMachineCodeButton);
            
            var purchaseHintText = new System.Windows.Controls.TextBlock()
            {
                Text = "获取注册码步骤：1. 复制机器码 → 2. 联系提供商 → 3. 获取注册码 → 4. 输入验证",
                FontSize = 10,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };
            machinePanel.Children.Add(purchaseHintText);
            
            // 添加购买指南超链接
            var purchaseGuideUrl = System.Configuration.ConfigurationManager.AppSettings["PurchaseGuideUrl"] ?? "http://38.181.35.75:8080/Guide";
            var purchaseGuideLink = new System.Windows.Documents.Hyperlink()
            {
                NavigateUri = new Uri(purchaseGuideUrl)
            };
            purchaseGuideLink.Inlines.Add("📖 购买注册码指南");
            purchaseGuideLink.RequestNavigate += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri.AbsoluteUri,
                        UseShellExecute = true
                    });
                    Console.WriteLine($"🌐 打开购买指南: {e.Uri.AbsoluteUri}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 打开购买指南失败: {ex.Message}");
                    MessageBox.Show($"无法打开购买指南：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                e.Handled = true;
            };
            
            var purchaseGuideLinkBlock = new System.Windows.Controls.TextBlock()
            {
                Margin = new Thickness(0, 0, 0, 0)
            };
            purchaseGuideLinkBlock.Inlines.Add(purchaseGuideLink);
            machinePanel.Children.Add(purchaseGuideLinkBlock);
            
            machineGroup.Content = machinePanel;
            mainPanel.Children.Add(machineGroup);
            
            // 注册码输入区域
            var licenseGroup = new System.Windows.Controls.GroupBox()
            {
                Header = "注册码验证",
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(5)
            };
            var licensePanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(10) };
            
            licensePanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "请输入注册码：", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5) 
            });
            
            var licenseKeyBox = new System.Windows.Controls.TextBox() 
            { 
                Height = 25, 
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 0, 0, 10) 
            };
            
            // 自动填入当前保存的注册码
            var currentLicenseKey = System.Configuration.ConfigurationManager.AppSettings["LicenseKey"];
            if (!string.IsNullOrEmpty(currentLicenseKey))
            {
                licenseKeyBox.Text = currentLicenseKey;
            }
            
            licensePanel.Children.Add(licenseKeyBox);
            
            var validateButton = new System.Windows.Controls.Button()
            {
                Content = "验证注册码",
                Width = 120,
                Height = 30,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            licensePanel.Children.Add(validateButton);
            
            var validationStatusText = new System.Windows.Controls.TextBlock()
            {
                Text = "",
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            licensePanel.Children.Add(validationStatusText);
            
            licenseGroup.Content = licensePanel;
            mainPanel.Children.Add(licenseGroup);
            
            // 底部按钮
            var buttonPanel = new System.Windows.Controls.StackPanel() 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 30, 0, 20)
            };
            
            var skipButton = new System.Windows.Controls.Button() 
            { 
                Content = "跳过验证（测试）", 
                Width = 130, 
                Height = 30,
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Colors.Orange),
                Foreground = new SolidColorBrush(Colors.White)
            };
            
            var exitButton = new System.Windows.Controls.Button() 
            { 
                Content = "退出应用", 
                Width = 90, 
                Height = 30,
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            buttonPanel.Children.Add(skipButton);
            buttonPanel.Children.Add(exitButton);
            mainPanel.Children.Add(buttonPanel);
            
            scrollViewer.Content = mainPanel;
            validationWindow.Content = scrollViewer;
            
            // 事件处理
            testServerButton.Click += async (s, e) =>
            {
                var serverUrl = serverAddressBox.Text.Trim();
                if (string.IsNullOrEmpty(serverUrl))
                {
                    serverStatusText.Text = "❌ 请输入服务器地址";
                    serverStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    return;
                }
                
                testServerButton.IsEnabled = false;
                testServerButton.Content = "测试中...";
                serverStatusText.Text = "🔄 正在测试服务器连接...";
                serverStatusText.Foreground = new SolidColorBrush(Colors.Blue);
                
                                 try
                 {
                     // 临时更新配置进行测试
                     var originalUrl = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"];
                     var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                     config.AppSettings.Settings["LicenseServerUrl"].Value = serverUrl;
                     config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                     System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                     
                     Console.WriteLine($"🔍 测试服务器连接: {serverUrl}");
                     
                     // 使用多种方式测试连接
                     bool connected = false;
                     string testResult = "";
                     
                     try
                     {
                         // 方式1: 使用LicenseManager的连接测试
                         connected = await LicenseManager.TestServerConnectionAsync();
                         testResult += $"LicenseManager测试: {(connected ? "成功" : "失败")}\n";
                         Console.WriteLine($"🔍 LicenseManager连接测试: {(connected ? "成功" : "失败")}");
                         
                         // 方式2: 如果LicenseManager测试失败，尝试简单的HTTP请求测试
                         if (!connected)
                         {
                             using (var httpClient = new System.Net.Http.HttpClient())
                             {
                                 httpClient.Timeout = TimeSpan.FromSeconds(10);
                                 var response = await httpClient.GetAsync(serverUrl);
                                 var httpConnected = response.IsSuccessStatusCode;
                                 testResult += $"HTTP测试: {(httpConnected ? "成功" : "失败")} (状态码: {response.StatusCode})";
                                 Console.WriteLine($"🔍 HTTP连接测试: {(httpConnected ? "成功" : "失败")} (状态码: {response.StatusCode})");
                                 
                                 if (httpConnected)
                                 {
                                     connected = true; // 如果HTTP能连接，认为服务器是可达的
                                 }
                             }
                         }
                     }
                     catch (Exception testEx)
                     {
                         testResult += $"连接测试异常: {testEx.Message}";
                         Console.WriteLine($"❌ 连接测试异常: {testEx.Message}");
                     }
                     
                     if (connected)
                     {
                         serverStatusText.Text = "✅ 服务器连接成功！\n\n" + testResult;
                         serverStatusText.Foreground = new SolidColorBrush(Colors.Green);
                         Console.WriteLine("✅ 服务器连接测试成功");
                     }
                     else
                     {
                         serverStatusText.Text = $"❌ 服务器连接失败\n\n{testResult}\n\n请检查：\n1. 服务器地址是否正确\n2. 服务器是否运行在正确端口\n3. 防火墙是否允许连接\n4. 网络连接是否正常";
                         serverStatusText.Foreground = new SolidColorBrush(Colors.Red);
                         Console.WriteLine($"❌ 服务器连接测试失败: {serverUrl}");
                     }
                     
                     // 如果测试失败，恢复原配置
                     if (!connected && !string.IsNullOrEmpty(originalUrl))
                     {
                         config.AppSettings.Settings["LicenseServerUrl"].Value = originalUrl;
                         config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                         System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                     }
                 }
                catch (Exception ex)
                {
                    serverStatusText.Text = $"❌ 连接测试失败：{ex.Message}";
                    serverStatusText.Foreground = new SolidColorBrush(Colors.Red);
                }
                finally
                {
                    testServerButton.IsEnabled = true;
                    testServerButton.Content = "测试服务器连接";
                }
            };
            
            copyMachineCodeButton.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(machineCode);
                    MessageBox.Show("机器码已复制到剪贴板！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            validateButton.Click += async (s, e) =>
            {
                var licenseKey = licenseKeyBox.Text.Trim();
                if (string.IsNullOrEmpty(licenseKey))
                {
                    validationStatusText.Text = "❌ 请输入注册码";
                    validationStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    return;
                }
                
                validateButton.IsEnabled = false;
                validateButton.Content = "验证中...";
                validationStatusText.Text = "🔄 正在验证注册码...";
                validationStatusText.Foreground = new SolidColorBrush(Colors.Blue);
                
                try
                {
                    // 1. 保存注册码到 AppData 目录（与程序更新分离）
                    Console.WriteLine($"💾 [1/2] 保存注册码到 AppData: {licenseKey}");
                    LicenseKeyStorage.SaveLicenseKey(licenseKey);
                    Console.WriteLine($"✅ AppData 保存成功: {LicenseKeyStorage.GetStoragePath()}");
                    
                    // 2. 同时保存到配置文件（LicenseManager 需要从这里读取）
                    Console.WriteLine($"💾 [2/2] 保存注册码到配置文件...");
                    var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                    config.AppSettings.Settings["LicenseKey"].Value = licenseKey;
                    config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                    System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                    Console.WriteLine($"✅ 配置文件保存成功");
                    
                    // 显示当前配置信息
                    var currentServerUrl = System.Configuration.ConfigurationManager.AppSettings["LicenseServerUrl"];
                    var currentAppId = System.Configuration.ConfigurationManager.AppSettings["ApplicationId"];
                    var currentLicenseKey = System.Configuration.ConfigurationManager.AppSettings["LicenseKey"];
                    
                    Console.WriteLine($"📋 当前配置信息:");
                    Console.WriteLine($"   服务器地址: {currentServerUrl}");
                    Console.WriteLine($"   应用程序ID: {currentAppId}");
                    Console.WriteLine($"   注册码: {currentLicenseKey}");
                    
                    // 重新初始化LicenseManager以确保使用最新配置
                    Console.WriteLine($"🔄 重新初始化LicenseManager...");
                    LicenseManager.Initialize(currentAppId ?? "BinanceApps2024", currentServerUrl);
                    Console.WriteLine($"✅ LicenseManager已重新初始化");
                    
                    // 获取机器码用于调试
                    var machineCode = LicenseManager.GetMachineCode();
                    Console.WriteLine($"🖥️ 机器码: {machineCode}");
                    
                    Console.WriteLine($"🚀 开始验证注册码...");
                    validationStatusText.Text += "\n正在连接服务器...";
                    
                    var result = await LicenseManager.ValidateCurrentLicenseAsync();
                    Console.WriteLine($"🔍 验证结果: IsValid={result.IsValid}, Message={result.Message}");
                    
                    // 尝试手动HTTP请求来验证服务器是否收到请求
                    Console.WriteLine($"🔍 尝试手动HTTP请求验证...");
                    try
                    {
                        using (var httpClient = new System.Net.Http.HttpClient())
                        {
                            httpClient.Timeout = TimeSpan.FromSeconds(30);
                            
                            // 构造验证请求（模拟SDK可能发送的请求）
                            var requestUrl = $"{currentServerUrl}/api/license/validate";
                            Console.WriteLine($"📡 请求URL: {requestUrl}");
                            
                            var requestData = new
                            {
                                ApplicationId = currentAppId,
                                LicenseKey = currentLicenseKey,
                                MachineCode = machineCode
                            };
                            
                            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestData);
                            var httpContent = new System.Net.Http.StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                            
                            Console.WriteLine($"📤 请求数据: {jsonContent}");
                            
                            var response = await httpClient.PostAsync(requestUrl, httpContent);
                            var responseContent = await response.Content.ReadAsStringAsync();
                            
                            Console.WriteLine($"📥 HTTP响应: 状态码={response.StatusCode}");
                            Console.WriteLine($"📥 响应内容: {responseContent}");
                            
                            validationStatusText.Text += $"\nHTTP测试: {response.StatusCode}\n响应: {responseContent}";
                        }
                    }
                    catch (Exception httpEx)
                    {
                        Console.WriteLine($"❌ HTTP请求异常: {httpEx.Message}");
                        validationStatusText.Text += $"\nHTTP测试失败: {httpEx.Message}";
                    }
                    
                    if (result.IsValid || result.Message.Contains("验证成功"))
                    {
                        validationStatusText.Text = "✅ 注册码验证成功！正在启动应用...";
                        validationStatusText.Foreground = new SolidColorBrush(Colors.Green);
                        
                        // 延迟1秒后启动主窗口
                        await Task.Delay(1000);
                        
                        var mainWindow = new MainWindow();
                        MainWindow = mainWindow;
                        mainWindow.Show();
                        Console.WriteLine("✅ 主窗口已启动");
                        
                        validationWindow.Close();
                    }
                    else
                    {
                        validationStatusText.Text = $"❌ SDK验证失败：{result.Message}\n\n注册码已保存，请查看上方HTTP测试结果。";
                        validationStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 验证异常: {ex.Message}");
                    validationStatusText.Text = $"❌ 验证失败：{ex.Message}\n\n注册码已保存，您可以稍后重试。";
                    validationStatusText.Foreground = new SolidColorBrush(Colors.Red);
                }
                finally
                {
                    validateButton.IsEnabled = true;
                    validateButton.Content = "验证注册码";
                }
            };
            
            skipButton.Click += (s, e) =>
            {
                var result = MessageBox.Show("跳过许可证验证将以测试模式启动应用程序。\n\n注意：这仅用于测试目的，正式使用需要有效的许可证。\n\n是否继续？", 
                                           "跳过验证确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    Console.WriteLine("⚠️ 用户跳过许可证验证，以测试模式启动");
                    var mainWindow = new MainWindow();
                    MainWindow = mainWindow;
                    mainWindow.Show();
                    mainWindow.Title += " - 测试模式（未验证许可证）";
                    Console.WriteLine("✅ 主窗口已启动（测试模式）");
                    validationWindow.Close();
                }
            };
            
            exitButton.Click += (s, e) =>
            {
                Console.WriteLine("❌ 用户退出应用");
                Shutdown();
            };
            
            validationWindow.ShowDialog();
        }

        private void ShowRegistrationDialog(string appName)
        {
            // 创建一个简单的输入对话框
            var inputDialog = new Window()
            {
                Title = "软件注册",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };
            
            var stackPanel = new System.Windows.Controls.StackPanel() { Margin = new Thickness(20) };
            
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock() 
            { 
                Text = "请输入您的注册码：", 
                Margin = new Thickness(0, 0, 0, 10) 
            });
            
            var textBox = new System.Windows.Controls.TextBox() 
            { 
                Height = 25, 
                Margin = new Thickness(0, 0, 0, 15) 
            };
            stackPanel.Children.Add(textBox);
            
            var buttonPanel = new System.Windows.Controls.StackPanel() 
            { 
                Orientation = System.Windows.Controls.Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right 
            };
            
            var okButton = new System.Windows.Controls.Button() 
            { 
                Content = "确定", 
                Width = 70, 
                Height = 25, 
                Margin = new Thickness(0, 0, 10, 0) 
            };
            
            var cancelButton = new System.Windows.Controls.Button() 
            { 
                Content = "取消", 
                Width = 70, 
                Height = 25 
            };
            
            okButton.Click += (s, e) => { inputDialog.DialogResult = true; inputDialog.Close(); };
            cancelButton.Click += (s, e) => { inputDialog.DialogResult = false; inputDialog.Close(); };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stackPanel.Children.Add(buttonPanel);
            
            inputDialog.Content = stackPanel;
            
            var result = inputDialog.ShowDialog();
            var licenseKey = textBox.Text;
            
            if (result == true && !string.IsNullOrEmpty(licenseKey))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        // 1. 保存注册码到 AppData 目录（与程序更新分离）
                        LicenseKeyStorage.SaveLicenseKey(licenseKey);
                        
                        // 2. 同时保存到配置文件（LicenseManager 需要从这里读取）
                        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                        config.AppSettings.Settings["LicenseKey"].Value = licenseKey;
                        config.Save(ConfigurationSaveMode.Modified);
                        ConfigurationManager.RefreshSection("appSettings");
                        
                        Console.WriteLine($"🔐 验证注册码: {licenseKey}");
                        var result = await LicenseManager.ValidateCurrentLicenseAsync();
                        
                        Dispatcher.Invoke(() =>
                        {
                            if (result.IsValid || result.Message.Contains("验证成功"))
                            {
                                Console.WriteLine("✅ 注册成功，启动主窗口");
                                var mainWindow = new MainWindow();
                                MainWindow = mainWindow;
                                mainWindow.Show();
                            }
                            else
                            {
                                MessageBox.Show($"注册失败：{result.Message}", "注册失败", MessageBoxButton.OK, MessageBoxImage.Error);
                                Shutdown();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"注册失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            Shutdown();
                        });
                    }
                });
            }
            else
            {
                Console.WriteLine("❌ 用户未输入注册码，应用程序退出");
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Console.WriteLine("🔄 应用程序正在关闭，正在清理资源...");
            
            try
            {
                LicenseManager.Cleanup();
                Console.WriteLine("✅ 许可证管理器已清理");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 清理许可证管理器时出错: {ex.Message}");
            }
            
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Console.WriteLine($"❌ 未处理的UI异常: {e.Exception.Message}");
            Console.WriteLine($"堆栈跟踪: {e.Exception.StackTrace}");
            
            MessageBox.Show($"应用程序遇到未处理的异常：\n{e.Exception.Message}\n\n程序将退出。",
                "未处理的异常", MessageBoxButton.OK, MessageBoxImage.Error);
            
            e.Handled = true;
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Console.WriteLine($"❌ 未处理的域异常: {ex.Message}");
                Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                
                MessageBox.Show($"应用程序遇到严重错误：\n{ex.Message}\n\n程序将退出。",
                    "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine($"❌ 未观察到的任务异常: {e.Exception.Message}");
            Console.WriteLine($"堆栈跟踪: {e.Exception.StackTrace}");
            
            e.SetObserved();
        }
        
        /// <summary>
        /// 从 AppData 加载注册码到配置文件
        /// 这样 LicenseManager 能读取到注册码，而更新时从 AppData 恢复
        /// </summary>
        private void LoadLicenseKeyFromAppData()
        {
            try
            {
                var licenseKey = LicenseKeyStorage.GetLicenseKey();
                if (!string.IsNullOrEmpty(licenseKey))
                {
                    // 将注册码从 AppData 恢复到配置文件
                    var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    if (config.AppSettings.Settings["LicenseKey"] != null)
                    {
                        config.AppSettings.Settings["LicenseKey"].Value = licenseKey;
                    }
                    else
                    {
                        config.AppSettings.Settings.Add("LicenseKey", licenseKey);
                    }
                    
                    // 保存到配置文件（LicenseManager 需要从文件读取）
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    
                    Console.WriteLine($"✅ 从 AppData 加载注册码成功");
                    Console.WriteLine($"📂 AppData 位置: {LicenseKeyStorage.GetStoragePath()}");
                    Console.WriteLine($"📝 已同步到配置文件");
                }
                else
                {
                    Console.WriteLine("ℹ️  AppData 中未找到保存的注册码");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  从 AppData 加载注册码失败: {ex.Message}");
            }
        }
    }
} 