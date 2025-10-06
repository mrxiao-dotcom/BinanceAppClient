# WPF 应用集成许可证验证和自动更新指南

## 📋 目录

1. [概述](#概述)
2. [功能 1：登录时注册和验证](#功能-1登录时注册和验证)
3. [功能 2：帮助菜单中加入检查更新](#功能-2帮助菜单中加入检查更新)
4. [功能 3：打包生成 ZIP 脚本](#功能-3打包生成-zip-脚本)
5. [功能 4：NuGet 包使用指南](#功能-4nuget-包使用指南)
6. [完整示例代码](#完整示例代码)
7. [常见问题与故障排查](#常见问题与故障排查)

---

## 概述

本指南将帮助您在 WPF 应用程序中集成：
- ✅ 许可证注册和在线验证
- ✅ 自动在线更新（智能更新）
- ✅ 一键打包发布脚本

### 依赖的 NuGet 包

1. **RegisterSrv.AutoUpdate** - 自动更新组件
2. **RegisterSrv.Client** - 许可证验证组件

### 服务器要求

需要部署 `RegisterSrv.Server` 服务器，提供：
- 许可证验证 API
- 版本检查 API
- 更新包下载 API

---

## 功能 1：登录时注册和验证

### 1.1 安装 NuGet 包

在 Visual Studio 中，打开 **程序包管理器控制台**：

```powershell
Install-Package RegisterSrv.Client
```

或在 `.csproj` 中添加：

```xml
<PackageReference Include="RegisterSrv.Client" Version="最新版本" />
```

### 1.2 创建 LicenseKeyStorage 类

用于在 AppData 中保存注册码，确保升级后不丢失。

**文件**: `LicenseKeyStorage.cs`

```csharp
using System;
using System.IO;

namespace YourApp
{
    /// <summary>
    /// 许可证密钥存储管理（保存在 AppData，避免升级丢失）
    /// </summary>
    public static class LicenseKeyStorage
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YourAppName"  // ← 修改为您的应用名称
        );

        private static readonly string LicenseFilePath = Path.Combine(AppDataFolder, "license.dat");

        /// <summary>
        /// 保存许可证密钥到 AppData
        /// </summary>
        public static void SaveLicenseKey(string licenseKey)
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                    Console.WriteLine($"✅ 创建 AppData 目录: {AppDataFolder}");
                }

                File.WriteAllText(LicenseFilePath, licenseKey);
                Console.WriteLine($"✅ 注册码已保存到: {LicenseFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 保存注册码失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 AppData 加载许可证密钥
        /// </summary>
        public static string LoadLicenseKey()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    var key = File.ReadAllText(LicenseFilePath).Trim();
                    Console.WriteLine($"✅ 从 AppData 加载注册码: {key}");
                    return key;
                }
                else
                {
                    Console.WriteLine($"⚠️  注册码文件不存在: {LicenseFilePath}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载注册码失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 清除保存的许可证密钥
        /// </summary>
        public static void ClearLicenseKey()
        {
            try
            {
                if (File.Exists(LicenseFilePath))
                {
                    File.Delete(LicenseFilePath);
                    Console.WriteLine("✅ 注册码已清除");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 清除注册码失败: {ex.Message}");
            }
        }
    }
}
```

### 1.3 配置 App.config

**文件**: `App.config`

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <appSettings>
        <!-- 服务器地址（许可证验证 + 自动更新） -->
        <add key="LicenseServerUrl" value="http://your-server:8080" />
        
        <!-- 应用程序唯一标识（在服务器上创建应用时生成） -->
        <add key="ApplicationId" value="App_YourAppId" />
        
        <!-- 应用程序名称 -->
        <add key="ApplicationName" value="YourAppName" />
        
        <!-- 当前版本（由更新管理器自动维护） -->
        <add key="CurrentAppVersion" value="1.0.0" />
        
        <!-- 许可证密钥（自动保存，但推荐保存在 AppData） -->
        <add key="LicenseKey" value="" />
    </appSettings>
</configuration>
```

### 1.4 修改 App.xaml.cs

**文件**: `App.xaml.cs`

```csharp
using System;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using RegisterSrv.Client;

namespace YourApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 读取配置
            string appId = ConfigurationManager.AppSettings["ApplicationId"] ?? "DefaultAppId";
            string appName = ConfigurationManager.AppSettings["ApplicationName"] ?? "YourApp";
            string serverUrl = ConfigurationManager.AppSettings["LicenseServerUrl"] ?? "http://localhost:8080";

            Console.WriteLine($"📋 应用信息: {appName} (ID: {appId})");
            Console.WriteLine($"🌐 服务器地址: {serverUrl}");

            // 从 AppData 加载注册码到内存
            LoadLicenseKeyFromAppData();

            // 初始化许可证管理器
            LicenseManager.Initialize(appId, serverUrl);
            Console.WriteLine("✅ 许可证管理器已初始化");

            base.OnStartup(e);

            // 后台验证许可证
            Console.WriteLine("🔐 开始后台许可证验证...");
            Task.Run(async () =>
            {
                try
                {
                    var result = await LicenseManager.ValidateCurrentLicenseAsync();
                    Console.WriteLine($"🔍 许可证验证结果: IsValid={result.IsValid}, Message={result.Message}");

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (!result.IsValid)
                        {
                            // 显示许可证输入对话框
                            Console.WriteLine("❌ 许可证验证失败，显示验证配置界面");
                            ShowLicenseInputDialog();
                        }
                        else
                        {
                            // 验证成功，显示主窗口
                            Console.WriteLine("✅ 许可证验证成功，显示主窗口");
                            ShowMainWindow();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 许可证验证异常: {ex.Message}");
                    await Dispatcher.InvokeAsync(() => ShowLicenseInputDialog());
                }
            });
        }

        /// <summary>
        /// 从 AppData 加载注册码到 App.config（内存）
        /// </summary>
        private void LoadLicenseKeyFromAppData()
        {
            try
            {
                var savedKey = LicenseKeyStorage.LoadLicenseKey();
                if (!string.IsNullOrWhiteSpace(savedKey))
                {
                    // 加载到内存配置（不写入 App.config 文件）
                    ConfigurationManager.AppSettings["LicenseKey"] = savedKey;
                    
                    // 同时保存到 App.config 文件（供 LicenseManager 读取）
                    SaveLicenseKeyToConfig(savedKey);
                }
                else
                {
                    Console.WriteLine("⚠️  AppData 中未找到保存的注册码");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 加载注册码失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存注册码到 App.config 文件
        /// </summary>
        private void SaveLicenseKeyToConfig(string licenseKey)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (configFile.AppSettings.Settings["LicenseKey"] != null)
                {
                    configFile.AppSettings.Settings["LicenseKey"].Value = licenseKey;
                    configFile.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    Console.WriteLine("✅ 注册码已同步到 App.config");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  保存注册码到 App.config 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示许可证输入对话框
        /// </summary>
        private void ShowLicenseInputDialog()
        {
            var dialog = new Window
            {
                Title = "许可证验证",
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new Thickness(20)
            };

            // 机器码
            var machineCode = LicenseManager.GetMachineCode();
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "机器码（用于申请许可证）:",
                Margin = new Thickness(0, 0, 0, 5)
            });
            var txtMachineCode = new System.Windows.Controls.TextBox
            {
                Text = machineCode,
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(txtMachineCode);

            // 注册码输入
            stackPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "请输入注册码:",
                Margin = new Thickness(0, 0, 0, 5)
            });
            var txtLicenseKey = new System.Windows.Controls.TextBox
            {
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(txtLicenseKey);

            // 验证按钮
            var btnVerify = new System.Windows.Controls.Button
            {
                Content = "验证并激活",
                Height = 35,
                Margin = new Thickness(0, 10, 0, 0)
            };

            btnVerify.Click += async (s, e) =>
            {
                var licenseKey = txtLicenseKey.Text.Trim();
                if (string.IsNullOrWhiteSpace(licenseKey))
                {
                    MessageBox.Show("请输入注册码！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                btnVerify.IsEnabled = false;
                btnVerify.Content = "验证中...";

                try
                {
                    // 保存到 AppData（持久化）
                    LicenseKeyStorage.SaveLicenseKey(licenseKey);
                    
                    // 保存到 App.config（供 LicenseManager 读取）
                    SaveLicenseKeyToConfig(licenseKey);

                    // 重新初始化并验证
                    var appId = ConfigurationManager.AppSettings["ApplicationId"];
                    var serverUrl = ConfigurationManager.AppSettings["LicenseServerUrl"];
                    LicenseManager.Initialize(appId, serverUrl);

                    var result = await LicenseManager.ValidateCurrentLicenseAsync();

                    if (result.IsValid)
                    {
                        MessageBox.Show($"许可证验证成功！\n{result.Message}", "成功", 
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        ShowMainWindow();
                    }
                    else
                    {
                        MessageBox.Show($"许可证验证失败！\n{result.Message}", "错误", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        btnVerify.IsEnabled = true;
                        btnVerify.Content = "验证并激活";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"验证过程出错：{ex.Message}", "错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    btnVerify.IsEnabled = true;
                    btnVerify.Content = "验证并激活";
                }
            };

            stackPanel.Children.Add(btnVerify);
            dialog.Content = stackPanel;
            dialog.ShowDialog();
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        private void ShowMainWindow()
        {
            if (MainWindow == null)
            {
                MainWindow = new MainWindow();
            }
            MainWindow.Show();
        }
    }
}
```

---

## 功能 2：帮助菜单中加入检查更新

### 2.1 安装 NuGet 包

```powershell
Install-Package RegisterSrv.AutoUpdate
```

### 2.2 创建 FixedUpdateManager 类

用于处理智能更新（只覆盖变更的文件，保护配置文件）。

**文件**: `FixedUpdateManager.cs`

```csharp
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using RegisterSrv.AutoUpdate;

namespace YourApp
{
    /// <summary>
    /// 固定的更新管理器 - 解决 URL 和智能更新问题
    /// </summary>
    public class FixedUpdateManager
    {
        private readonly UpdateConfig _config;
        private readonly UpdateClient _client;
        private readonly HttpClient _httpClient;

        public FixedUpdateManager(UpdateConfig config)
        {
            _config = config;
            _client = new UpdateClient(config);

            // 创建 HttpClient 并设置 BaseAddress
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(config.ServerUrl),
                Timeout = TimeSpan.FromMinutes(10)
            };

            Console.WriteLine($"✅ FixedUpdateManager 已初始化");
            Console.WriteLine($"   BaseAddress: {_httpClient.BaseAddress}");
        }

        /// <summary>
        /// 检查并执行更新
        /// </summary>
        public async Task CheckAndUpdateAsync(Window owner, bool silent = false)
        {
            try
            {
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("🔍 开始检查更新");
                Console.WriteLine($"   服务器: {_config.ServerUrl}");
                Console.WriteLine($"   应用ID: {_config.AppId}");
                Console.WriteLine($"   应用名称: {_config.AppName}");
                Console.WriteLine($"   当前版本: {_config.CurrentVersion}");

                // 检查更新
                var checkUrl = $"{_config.ServerUrl}/api/update/check?appId={_config.AppId}&currentVersion={_config.CurrentVersion}";
                Console.WriteLine($"   检查 URL: {checkUrl}");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                var response = await _httpClient.GetAsync(checkUrl);
                var updateInfo = await response.Content.ReadAsAsync<UpdateInfo>();

                if (!updateInfo.HasUpdate)
                {
                    if (!silent)
                    {
                        MessageBox.Show($"当前已是最新版本 {_config.CurrentVersion}", "检查更新",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                Console.WriteLine($"🆕 发现新版本: {updateInfo.Version}");
                Console.WriteLine($"📥 下载 URL: '{updateInfo.DownloadUrl}'");

                // 如果 MD5 为空，先下载并计算
                if (string.IsNullOrWhiteSpace(updateInfo.Md5))
                {
                    Console.WriteLine("⚠️  警告：服务器未提供 MD5");
                    Console.WriteLine("🔄 将先下载文件并计算实际 MD5 值");

                    var preDownloadResult = await PreDownloadAndCalculateMd5(updateInfo);
                    if (!preDownloadResult.Success)
                    {
                        throw new Exception($"预下载失败: {preDownloadResult.ErrorMessage}");
                    }

                    updateInfo.Md5 = preDownloadResult.Md5;
                    Console.WriteLine($"✅ 计算的 MD5: {updateInfo.Md5}");
                }

                // 询问用户是否更新
                var result = MessageBox.Show(
                    $"发现新版本: {updateInfo.Version}\n\n" +
                    $"当前版本: {_config.CurrentVersion}\n" +
                    $"新版本: {updateInfo.Version}\n" +
                    $"更新内容: {updateInfo.ReleaseNotes}\n\n" +
                    $"是否立即更新？",
                    "发现新版本",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                // 下载并安装（智能更新）
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("📦  开始智能更新...");
                var success = await SmartInstallUpdateAsync(updateInfo);

                if (success)
                {
                    // 保存新版本号到 App.config
                    SaveCurrentVersion(updateInfo.Version);

                    MessageBox.Show(
                        $"更新成功！\n\n应用程序将重启以完成更新。",
                        "更新成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 重启应用
                    System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                    Application.Current.Shutdown();
                }
                else
                {
                    throw new Exception("智能更新安装失败");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("❌ 更新过程异常:");
                Console.WriteLine($"   类型: {ex.GetType().Name}");
                Console.WriteLine($"   消息: {ex.Message}");
                Console.WriteLine($"   堆栈: {ex.StackTrace}");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                if (!silent)
                {
                    MessageBox.Show($"更新失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 预下载文件并计算 MD5
        /// </summary>
        private async Task<(bool Success, string Md5, string ErrorMessage)> PreDownloadAndCalculateMd5(UpdateInfo updateInfo)
        {
            string tempFile = null;
            try
            {
                var downloadUrl = updateInfo.DownloadUrl.StartsWith("http")
                    ? updateInfo.DownloadUrl
                    : $"{_config.ServerUrl}{updateInfo.DownloadUrl}";

                tempFile = Path.Combine(Path.GetTempPath(), $"update_{updateInfo.Version}_{Guid.NewGuid()}.zip");

                Console.WriteLine("📥 预下载文件以计算 MD5...");
                Console.WriteLine($"   下载地址: {downloadUrl}");
                Console.WriteLine($"   临时位置: {tempFile}");

                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    await stream.CopyToAsync(fs);
                }

                Console.WriteLine("✅ 预下载完成！");

                // 计算 MD5
                Console.WriteLine("🔐 计算文件 MD5...");
                string md5;
                using (var md5Hash = MD5.Create())
                using (var stream = File.OpenRead(tempFile))
                {
                    var hash = md5Hash.ComputeHash(stream);
                    md5 = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }

                return (true, md5, null);
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
            finally
            {
                if (tempFile != null && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        /// <summary>
        /// 智能安装更新（只覆盖变更的文件）
        /// </summary>
        private async Task<bool> SmartInstallUpdateAsync(UpdateInfo updateInfo)
        {
            string tempZipFile = null;
            string tempExtractDir = null;

            try
            {
                // 1. 下载更新包
                var downloadUrl = updateInfo.DownloadUrl.StartsWith("http")
                    ? updateInfo.DownloadUrl
                    : $"{_config.ServerUrl}{updateInfo.DownloadUrl}";

                tempZipFile = Path.Combine(Path.GetTempPath(), $"update_{updateInfo.Version}_{Guid.NewGuid()}.zip");
                tempExtractDir = Path.Combine(Path.GetTempPath(), $"update_extract_{Guid.NewGuid()}");

                Console.WriteLine($"📥 下载更新包到: {tempZipFile}");
                var response = await _httpClient.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(tempZipFile, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }

                Console.WriteLine("✅ 下载完成");

                // 2. 解压到临时目录
                Console.WriteLine($"📂 解压到: {tempExtractDir}");
                Directory.CreateDirectory(tempExtractDir);
                ZipFile.ExtractToDirectory(tempZipFile, tempExtractDir);

                // 3. 智能复制（只覆盖变更的文件）
                var targetDir = AppDomain.CurrentDomain.BaseDirectory;
                Console.WriteLine($"🔄 开始智能复制到: {targetDir}");

                var protectedPatterns = new[] { "App.config", "*.db", "*.log", "appsettings.json" };
                var copiedCount = 0;
                var skippedCount = 0;

                foreach (var sourceFile in Directory.GetFiles(tempExtractDir, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(tempExtractDir, sourceFile);
                    var targetFile = Path.Combine(targetDir, relativePath);

                    // 检查是否是受保护的文件
                    var isProtected = protectedPatterns.Any(pattern =>
                    {
                        if (pattern.Contains("*"))
                        {
                            var extension = pattern.Replace("*", "");
                            return relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
                        }
                        return relativePath.Equals(pattern, StringComparison.OrdinalIgnoreCase);
                    });

                    if (isProtected)
                    {
                        Console.WriteLine($"🛡️  跳过受保护文件: {relativePath}");
                        skippedCount++;
                        continue;
                    }

                    // 比较文件内容（MD5）
                    var shouldCopy = true;
                    if (File.Exists(targetFile))
                    {
                        var sourceMd5 = CalculateFileMd5(sourceFile);
                        var targetMd5 = CalculateFileMd5(targetFile);

                        if (sourceMd5 == targetMd5)
                        {
                            Console.WriteLine($"⏭️  跳过相同文件: {relativePath}");
                            skippedCount++;
                            shouldCopy = false;
                        }
                    }

                    if (shouldCopy)
                    {
                        var targetFileDir = Path.GetDirectoryName(targetFile);
                        if (!Directory.Exists(targetFileDir))
                        {
                            Directory.CreateDirectory(targetFileDir);
                        }

                        File.Copy(sourceFile, targetFile, true);
                        Console.WriteLine($"✅ 复制: {relativePath}");
                        copiedCount++;
                    }
                }

                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"✅ 智能更新完成");
                Console.WriteLine($"   复制文件: {copiedCount}");
                Console.WriteLine($"   跳过文件: {skippedCount}");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 智能更新失败: {ex.Message}");
                return false;
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (tempZipFile != null && File.Exists(tempZipFile))
                        File.Delete(tempZipFile);
                    if (tempExtractDir != null && Directory.Exists(tempExtractDir))
                        Directory.Delete(tempExtractDir, true);
                }
                catch { }
            }
        }

        /// <summary>
        /// 计算文件 MD5
        /// </summary>
        private string CalculateFileMd5(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// 保存当前版本号到 App.config
        /// </summary>
        private void SaveCurrentVersion(string version)
        {
            try
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);

                if (config.AppSettings.Settings["CurrentAppVersion"] != null)
                {
                    config.AppSettings.Settings["CurrentAppVersion"].Value = version;
                }
                else
                {
                    config.AppSettings.Settings.Add("CurrentAppVersion", version);
                }

                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");

                Console.WriteLine($"✅ 版本号已更新到: {version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  保存版本号失败: {ex.Message}");
            }
        }
    }
}
```

### 2.3 在 App.xaml.cs 中初始化更新管理器

在 `OnStartup` 方法中添加：

```csharp
// 初始化自动更新管理器
var updateConfig = new UpdateConfig
{
    ServerUrl = serverUrl,  // 从 App.config 读取
    AppId = appId,
    AppName = appName,
    CurrentVersion = GetApplicationVersion(),
    AutoCheckOnStartup = false,  // 不在启动时自动检查
    SilentUpdate = false
};

UpdateManager = new FixedUpdateManager(updateConfig);
Console.WriteLine($"✅ 自动更新管理器已初始化 (版本: {updateConfig.CurrentVersion})");

// GetApplicationVersion 方法
private string GetApplicationVersion()
{
    // 优先使用 App.config 中的版本（由更新管理器维护）
    var configVersion = ConfigurationManager.AppSettings["CurrentAppVersion"];
    if (!string.IsNullOrWhiteSpace(configVersion))
    {
        return configVersion;
    }

    // 否则使用程序集版本
    var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
    return $"{version.Major}.{version.Minor}.{version.Build}";
}
```

### 2.4 在 MainWindow.xaml 中添加菜单

```xml
<Menu DockPanel.Dock="Top">
    <MenuItem Header="帮助(_H)">
        <MenuItem Header="检查更新(_U)" Click="MenuItem_CheckUpdate_Click" />
        <Separator />
        <MenuItem Header="关于(_A)" Click="MenuItem_About_Click" />
    </MenuItem>
</Menu>
```

### 2.5 在 MainWindow.xaml.cs 中实现检查更新

```csharp
/// <summary>
/// 检查更新
/// </summary>
private async void MenuItem_CheckUpdate_Click(object sender, RoutedEventArgs e)
{
    try
    {
        var app = (App)Application.Current;
        await app.UpdateManager.CheckAndUpdateAsync(this, silent: false);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"检查更新失败：{ex.Message}", "错误",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

/// <summary>
/// 关于
/// </summary>
private void MenuItem_About_Click(object sender, RoutedEventArgs e)
{
    var version = ((App)Application.Current).GetApplicationVersion();
    MessageBox.Show(
        $"应用名称: {ConfigurationManager.AppSettings["ApplicationName"]}\n" +
        $"版本: {version}\n" +
        $"服务器: {ConfigurationManager.AppSettings["LicenseServerUrl"]}",
        "关于",
        MessageBoxButton.OK,
        MessageBoxImage.Information);
}
```

---

## 功能 3：打包生成 ZIP 脚本

### 3.1 创建发布脚本

**文件**: `快速打包更新.cmd`

```batch
@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo 快速打包更新（智能更新专用）
echo ========================================
echo.

:: 配置路径（根据您的项目结构调整）
set PUBLISH_DIR=src\YourAppName.WPF\publish
set ZIP_NAME=YourAppName_v1.0.0.zip

echo 📋 打包配置：
echo   源目录: %PUBLISH_DIR%
echo   输出文件: %ZIP_NAME%
echo.

:: 步骤 1：检查 publish 目录是否存在
echo 🔍 步骤 1/3：检查发布目录...
if not exist "%PUBLISH_DIR%" (
    echo.
    echo ❌ 错误：publish 目录不存在！
    echo.
    echo 📍 期望位置: %CD%\%PUBLISH_DIR%
    echo.
    echo 💡 解决方法：
    echo    1. 在 VS2022 中右键点击项目
    echo    2. 选择 "发布"
    echo    3. 确保发布到 publish 目录
    echo.
    pause
    exit /b 1
)

:: 检查必需文件
if not exist "%PUBLISH_DIR%\YourAppName.WPF.exe" (
    echo.
    echo ❌ 错误：publish 目录中没有找到 YourAppName.WPF.exe
    echo.
    echo 💡 请先在 VS2022 中发布项目
    echo.
    pause
    exit /b 1
)

echo ✅ 发布目录检查通过
echo.

:: 步骤 2：清理旧的 ZIP 文件
echo 🗑️  步骤 2/3：清理旧文件...
if exist "%ZIP_NAME%" (
    del /f /q "%ZIP_NAME%"
    echo ✅ 已删除旧的 ZIP 文件
) else (
    echo ℹ️  没有旧文件需要清理
)
echo.

:: 步骤 3：打包 ZIP（包含所有文件）
echo 📦 步骤 3/3：打包 ZIP...
pushd %PUBLISH_DIR%
powershell -Command "Compress-Archive -Path '*' -DestinationPath '..\..\%ZIP_NAME%' -Force"
popd

:: 检查打包结果
if exist "%ZIP_NAME%" (
    echo.
    echo ========================================
    echo ✅ 打包成功！
    echo ========================================
    echo.
    echo 📍 ZIP 文件位置: %CD%\%ZIP_NAME%
    
    :: 显示文件大小
    for %%A in (%ZIP_NAME%) do (
        set /a SIZE_BYTES=%%~zA
        set /a SIZE_KB=!SIZE_BYTES!/1024
        set /a SIZE_MB=!SIZE_BYTES!/1024/1024
        echo 📏 文件大小: !SIZE_BYTES! 字节 ^(!SIZE_KB! KB / !SIZE_MB! MB^)
    )
    
    echo.
    echo 💡 提示：
    echo    ✅ 注册码保存在 AppData 目录，更新不影响注册码
    echo    ✅ 智能更新只覆盖变更的文件
    echo.
    echo 📋 下一步：
    echo    1. 验证 ZIP 文件内容
    echo    2. 上传到更新服务器
    echo    3. 在客户端测试更新功能
    echo.
    
    :: 自动打开文件资源管理器
    explorer /select,"%CD%\%ZIP_NAME%"
) else (
    echo.
    echo ========================================
    echo ❌ 打包失败！
    echo ========================================
    echo.
    echo 可能原因：
    echo   - PowerShell 执行权限不足
    echo   - publish 目录为空
    echo   - 磁盘空间不足
    echo.
)

pause
```

### 3.2 使用方法

1. 在 VS2022 中发布项目（右键项目 → 发布）
2. 运行 `快速打包更新.cmd`
3. 生成的 ZIP 文件会自动在资源管理器中打开

### 3.3 版本号管理

每次发布新版本时，需要同步更新 3 个地方的版本号：

1. **项目文件** (`YourApp.csproj`)
```xml
<Version>1.0.1</Version>
<AssemblyVersion>1.0.1.0</AssemblyVersion>
<FileVersion>1.0.1.0</FileVersion>
```

2. **配置文件** (`App.config`)
```xml
<add key="CurrentAppVersion" value="1.0.1" />
```

3. **打包脚本** (`快速打包更新.cmd`)
```batch
set ZIP_NAME=YourAppName_v1.0.1.zip
```

---

## 功能 4：NuGet 包使用指南

### 4.1 RegisterSrv.Client（许可证验证）

#### 安装

```powershell
Install-Package RegisterSrv.Client
```

#### 核心类和方法

##### 1. LicenseManager

```csharp
// 初始化
LicenseManager.Initialize(string appId, string serverUrl);

// 获取机器码
string machineCode = LicenseManager.GetMachineCode();

// 验证当前许可证
var result = await LicenseManager.ValidateCurrentLicenseAsync();
// result.IsValid - 是否有效
// result.Message - 验证消息

// 激活许可证
var activateResult = await LicenseManager.ActivateLicenseAsync(
    string licenseKey,
    string machineCode
);
```

##### 2. ValidationResult

```csharp
public class ValidationResult
{
    public bool IsValid { get; set; }        // 是否有效
    public string Message { get; set; }      // 消息
    public DateTime? ExpiryDate { get; set; } // 过期日期
    public string LicenseType { get; set; }  // 许可证类型
}
```

#### 使用流程

```
1. Initialize() ← 初始化管理器
2. GetMachineCode() ← 获取机器码（用于激活）
3. ActivateLicenseAsync() ← 激活许可证
4. ValidateCurrentLicenseAsync() ← 验证许可证
```

### 4.2 RegisterSrv.AutoUpdate（自动更新）

#### 安装

```powershell
Install-Package RegisterSrv.AutoUpdate
```

#### 核心类和方法

##### 1. UpdateConfig

```csharp
var config = new UpdateConfig
{
    ServerUrl = "http://your-server:8080",  // 服务器地址
    AppId = "App_YourAppId",                // 应用ID
    AppName = "YourApp",                    // 应用名称
    CurrentVersion = "1.0.0",               // 当前版本
    AutoCheckOnStartup = false,             // 是否启动时检查
    SilentUpdate = false                    // 是否静默更新
};
```

##### 2. UpdateClient

```csharp
var client = new UpdateClient(config);

// 检查更新
var updateInfo = await client.CheckForUpdateAsync();
// updateInfo.HasUpdate - 是否有更新
// updateInfo.Version - 新版本号
// updateInfo.DownloadUrl - 下载地址
// updateInfo.Md5 - MD5 校验值
// updateInfo.ReleaseNotes - 更新说明

// 下载更新
await client.DownloadUpdateAsync(updateInfo, progressCallback);

// 安装更新
client.InstallUpdate(zipFilePath, targetDirectory);
```

##### 3. UpdateInfo

```csharp
public class UpdateInfo
{
    public bool HasUpdate { get; set; }         // 是否有更新
    public string Version { get; set; }         // 版本号
    public string DownloadUrl { get; set; }     // 下载地址
    public long FileSize { get; set; }          // 文件大小
    public string Md5 { get; set; }             // MD5 校验
    public string ReleaseNotes { get; set; }    // 更新说明
    public bool ForceUpdate { get; set; }       // 是否强制更新
    public DateTime ReleaseDate { get; set; }   // 发布日期
}
```

#### 使用流程

```
1. new UpdateConfig() ← 配置更新参数
2. new UpdateClient(config) ← 创建更新客户端
3. CheckForUpdateAsync() ← 检查是否有更新
4. DownloadUpdateAsync() ← 下载更新包
5. InstallUpdate() ← 安装更新
6. 重启应用程序
```

### 4.3 最佳实践

#### 1. 许可证保存在 AppData

```csharp
// ✅ 推荐：保存在 AppData
var appDataPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "YourAppName",
    "license.dat"
);
File.WriteAllText(appDataPath, licenseKey);

// ❌ 不推荐：保存在程序目录
// 更新时会被覆盖
```

#### 2. 智能更新（只覆盖变更文件）

```csharp
// ✅ 推荐：使用 FixedUpdateManager
// 自动比较文件 MD5，只覆盖变更的文件
// 自动保护 App.config、*.db、*.log 等文件

// ❌ 不推荐：使用原生 UpdateClient
// 会覆盖所有文件，包括配置和数据
```

#### 3. 版本号管理

```csharp
// ✅ 推荐：从 App.config 读取，由更新管理器维护
var version = ConfigurationManager.AppSettings["CurrentAppVersion"];

// ❌ 不推荐：从程序集版本读取
// 每次编译都需要手动更新
var version = Assembly.GetExecutingAssembly().GetName().Version;
```

#### 4. 服务器地址配置

```csharp
// ✅ 推荐：从 App.config 动态读取
ServerUrl = ConfigurationManager.AppSettings["LicenseServerUrl"];

// ❌ 不推荐：硬编码
ServerUrl = "http://192.168.1.101:8080";  // 无法灵活切换
```

---

## 完整示例代码

### 项目结构

```
YourApp/
├── App.xaml
├── App.xaml.cs              ← 应用启动、许可证验证、更新管理器初始化
├── App.config               ← 服务器地址、应用ID、版本号
├── MainWindow.xaml          ← 主窗口UI
├── MainWindow.xaml.cs       ← 检查更新菜单
├── LicenseKeyStorage.cs     ← 注册码保存到 AppData
├── FixedUpdateManager.cs    ← 智能更新管理器
└── YourApp.csproj           ← 项目文件、版本号
```

### 完整的 .csproj 示例

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    
    <!-- 版本信息 -->
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
    
    <!-- 应用信息 -->
    <ApplicationName>YourAppName</ApplicationName>
    <Company>Your Company</Company>
    <Product>YourAppName</Product>
    <Copyright>Copyright © 2025</Copyright>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="RegisterSrv.Client" Version="*" />
    <PackageReference Include="RegisterSrv.AutoUpdate" Version="*" />
  </ItemGroup>
</Project>
```

---

## 常见问题与故障排查

### 问题 1：许可证验证失败

**症状**：提示"未找到保存的注册码"

**原因**：
1. 注册码未保存到 AppData
2. `LicenseManager` 未正确初始化
3. 服务器地址错误

**解决**：
```csharp
// 确保保存到 AppData
LicenseKeyStorage.SaveLicenseKey(licenseKey);

// 确保同步到 App.config（供 LicenseManager 读取）
SaveLicenseKeyToConfig(licenseKey);

// 重新初始化
LicenseManager.Initialize(appId, serverUrl);
```

### 问题 2：检查更新失败

**症状**：提示"无效的请求 URI"

**原因**：
1. 服务器地址硬编码
2. `HttpClient` 未设置 `BaseAddress`

**解决**：
```csharp
// ✅ 使用 FixedUpdateManager
// 已自动设置 BaseAddress

// ✅ 从 App.config 读取服务器地址
ServerUrl = ConfigurationManager.AppSettings["LicenseServerUrl"];
```

### 问题 3：更新后配置丢失

**症状**：更新后需要重新输入注册码

**原因**：
1. 注册码保存在程序目录的 `App.config`
2. 更新时被覆盖

**解决**：
```csharp
// ✅ 保存注册码到 AppData
LicenseKeyStorage.SaveLicenseKey(licenseKey);

// ✅ 使用智能更新（FixedUpdateManager）
// 自动保护 App.config、*.db、*.log
```

### 问题 4：MD5 校验失败

**症状**：下载完成后提示 MD5 校验失败

**原因**：
1. 服务器返回的 MD5 为空
2. 文件在传输过程中损坏

**解决**：
```csharp
// ✅ 使用 FixedUpdateManager
// 自动检测空 MD5，预下载并计算实际 MD5

// 服务器端也应该正确计算并返回 MD5
```

### 问题 5：版本号不更新

**症状**：更新后"关于"中显示的仍是旧版本

**原因**：
1. 版本号从程序集读取，未更新 `App.config`
2. `GetApplicationVersion` 方法未优先使用 `App.config`

**解决**：
```csharp
// ✅ 优先从 App.config 读取
private string GetApplicationVersion()
{
    var configVersion = ConfigurationManager.AppSettings["CurrentAppVersion"];
    if (!string.IsNullOrWhiteSpace(configVersion))
    {
        return configVersion;
    }
    // 否则使用程序集版本
    var version = Assembly.GetExecutingAssembly().GetName().Version;
    return $"{version.Major}.{version.Minor}.{version.Build}";
}

// ✅ 更新后保存新版本号
SaveCurrentVersion(updateInfo.Version);
```

---

## 发布和测试流程

### 1. 开发阶段

```
1. 修改代码
2. 更新版本号（.csproj, App.config, 打包脚本）
3. 在 VS2022 中调试测试
```

### 2. 发布阶段

```
1. 在 VS2022 中右键项目 → 发布
2. 运行 快速打包更新.cmd
3. 生成 ZIP 文件
```

### 3. 部署阶段

```
1. 登录更新服务器管理后台
2. 创建新版本
3. 上传 ZIP 文件
4. 填写更新说明
```

### 4. 测试阶段

```
1. 在客户端运行旧版本
2. 点击"帮助" → "检查更新"
3. 确认提示新版本
4. 点击"立即更新"
5. 等待下载和安装
6. 确认自动重启
7. 验证新版本功能
8. 确认注册码仍然有效
9. 确认配置文件未丢失
```

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0 | 2025-10-01 | 初始版本 |

---

## 相关文档

- `服务器地址配置说明.md` - 服务器地址修改指南
- `构建目录说明.md` - 构建输出目录说明
- `发布和打包工作流程.md` - 发布流程详解

---

## 技术支持

如有问题，请参考：
1. 本文档的"常见问题与故障排查"部分
2. 查看应用程序控制台输出的详细日志
3. 检查服务器端日志

---

**文档版本**: 1.0  
**更新日期**: 2025-10-01  
**适用框架**: .NET 9.0, WPF  
**NuGet 包**: RegisterSrv.Client, RegisterSrv.AutoUpdate 