# RegisterSrv.ClientSDK 使用教程

> **最新版本**: v1.0.1  
> **发布日期**: 2025-10-21  
> **适用平台**: .NET 9.0 Windows

---

## 📦 目录

1. [快速开始](#快速开始)
2. [安装 SDK](#安装-sdk)
3. [基础配置](#基础配置)
4. [注册码验证](#注册码验证)
5. [使用注册窗口](#使用注册窗口)
6. [机器码管理](#机器码管理)
7. [离线模式](#离线模式)
8. [完整示例](#完整示例)
9. [最佳实践](#最佳实践)
10. [常见问题](#常见问题)
11. [API 参考](#api-参考)

---

## 快速开始

### 5 分钟快速集成

```csharp
// 1. 安装 NuGet 包
// Install-Package RegisterSrv.ClientSDK

// 2. 在启动时显示注册窗口
using RegisterSrv.ClientSDK;

var licenseWindow = new LicenseWindow(
    appId: "YourAppId",           // 您的应用ID
    appName: "我的应用程序",        // 应用名称
    appVersion: "1.0.0",          // 应用版本
    serverUrl: "http://your-server:5232"  // 服务器地址（可选）
);

if (licenseWindow.ShowDialog() == true)
{
    if (licenseWindow.IsLicenseValid)
    {
        // 验证成功，启动主程序
        Application.Run(new MainForm());
    }
    else
    {
        // 验证失败，退出
        MessageBox.Show("许可证验证失败！");
        Environment.Exit(1);
    }
}
```

---

## 安装 SDK

### 方式 1：使用 NuGet 包管理器（推荐）

#### Visual Studio
1. 右键点击项目 → "管理 NuGet 包"
2. 搜索 `RegisterSrv.ClientSDK`
3. 点击"安装"

#### Package Manager Console
```powershell
Install-Package RegisterSrv.ClientSDK -Version 1.0.1
```

#### .NET CLI
```bash
dotnet add package RegisterSrv.ClientSDK --version 1.0.1
```

### 方式 2：编辑 .csproj 文件

```xml
<ItemGroup>
  <PackageReference Include="RegisterSrv.ClientSDK" Version="1.0.1" />
</ItemGroup>
```

### 验证安装

```csharp
using RegisterSrv.ClientSDK;
using RegisterSrv.ClientSDK.Services;
using RegisterSrv.ClientSDK.Config;

// 如果能正常编译，说明安装成功
```

---

## 基础配置

### 配置文件设置

SDK 支持多种配置方式，优先级从高到低：

#### 1. JSON 配置文件（推荐）⭐

在应用程序根目录创建 `registersrv.json`：

```json
{
  "ServerUrl": "http://localhost:5232",
  "TimeoutSeconds": 30,
  "EnableOfflineMode": true,
  "OfflineCacheHours": 24,
  "RetryCount": 3
}
```

#### 2. App.config 配置

```xml
<configuration>
  <appSettings>
    <add key="RegisterSrv.ServerUrl" value="http://localhost:5232" />
    <add key="RegisterSrv.TimeoutSeconds" value="30" />
    <add key="RegisterSrv.EnableOfflineMode" value="true" />
    <add key="RegisterSrv.OfflineCacheHours" value="24" />
    <add key="RegisterSrv.RetryCount" value="3" />
  </appSettings>
</configuration>
```

#### 3. 代码配置

```csharp
using RegisterSrv.ClientSDK.Config;

// 更新服务器地址
ClientConfig.Instance.UpdateServerUrl("http://your-server:5232");

// 创建默认配置文件
ClientConfig.CreateDefaultConfigFile("http://your-server:5232");
```

### 配置项说明

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `ServerUrl` | string | `http://localhost:5232` | 许可证服务器地址 |
| `TimeoutSeconds` | int | `30` | 网络请求超时时间（秒） |
| `EnableOfflineMode` | bool | `true` | 是否启用离线模式 |
| `OfflineCacheHours` | int | `24` | 离线缓存有效期（小时） |
| `RetryCount` | int | `3` | 失败重试次数 |

---

## 注册码验证

### 方式 1：使用 LicenseClient（编程方式）

#### 基础验证

```csharp
using RegisterSrv.ClientSDK.Services;

public async Task<bool> ValidateLicenseAsync(string keyCode)
{
    // 创建客户端（自动读取配置）
    using var client = new LicenseClient("YourAppId");
    
    // 验证注册码
    var result = await client.ValidateAsync(keyCode, appVersion: "1.0.0");
    
    if (result.IsValid)
    {
        Console.WriteLine("✅ 验证成功！");
        Console.WriteLine($"许可类型: {result.LicenseType}");
        
        // ✅ v1.0.1 新功能：获取剩余天数
        if (result.RemainingDays.HasValue)
        {
            Console.WriteLine($"剩余天数: {result.RemainingDays.Value} 天");
            
            if (result.IsExpiringSoon)
            {
                Console.WriteLine("⚠️ 警告：许可证即将过期（剩余7天内）！");
            }
        }
        else
        {
            Console.WriteLine("许可类型: 永久许可");
        }
        
        return true;
    }
    else
    {
        Console.WriteLine($"❌ 验证失败: {result.Message}");
        return false;
    }
}
```

#### 带服务器地址的验证

```csharp
// 指定服务器地址
using var client = new LicenseClient(
    baseUrl: "http://your-server:5232", 
    appId: "YourAppId"
);

var result = await client.ValidateAsync("YOUR-LICENSE-KEY");
```

#### 激活注册码

```csharp
// 首次激活（绑定机器码）
var activationResult = await client.ActivateAsync("YOUR-LICENSE-KEY");

if (activationResult.IsSuccess)
{
    Console.WriteLine("✅ 激活成功！");
    
    // 激活后验证
    var validateResult = await client.ValidateAsync("YOUR-LICENSE-KEY");
}
else
{
    Console.WriteLine($"❌ 激活失败: {activationResult.Message}");
}
```

### 方式 2：使用 LicenseWindow（UI 方式）⭐

#### 在 WPF 应用中使用

```csharp
using System.Windows;
using RegisterSrv.ClientSDK;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // 显示注册窗口
        var licenseWindow = new LicenseWindow(
            appId: "YourAppId",
            appName: "我的应用程序",
            appVersion: "1.0.0"
        );
        
        licenseWindow.Owner = MainWindow;  // 设置父窗口
        
        if (licenseWindow.ShowDialog() == true)
        {
            if (licenseWindow.IsLicenseValid)
            {
                // 获取已验证的注册码
                string validatedKey = licenseWindow.ValidatedLicenseKey;
                
                // 启动主窗口
                MainWindow = new MainWindow();
                MainWindow.Show();
            }
            else
            {
                // 验证失败，退出应用
                Shutdown();
            }
        }
        else
        {
            // 用户取消，退出应用
            Shutdown();
        }
    }
}
```

#### 在 WinForms 应用中使用

```csharp
using System;
using System.Windows.Forms;
using RegisterSrv.ClientSDK;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        
        // 显示注册窗口
        var licenseWindow = new LicenseWindow(
            appId: "YourAppId",
            appName: "我的应用程序",
            appVersion: "1.0.0",
            serverUrl: "http://your-server:5232"
        );
        
        if (licenseWindow.ShowDialog() == true)
        {
            if (licenseWindow.IsLicenseValid)
            {
                // 验证成功，运行主窗体
                Application.Run(new MainForm());
            }
            else
            {
                MessageBox.Show("许可证验证失败！", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
```

---

## 机器码管理

### 获取机器码

```csharp
using RegisterSrv.ClientSDK.Services;

// 获取当前机器的唯一标识码
using var client = new LicenseClient("YourAppId");
string machineCode = client.GetMachineCode();

Console.WriteLine($"机器码: {machineCode}");
// 输出示例: 7F9E2C8B3D4A5E1C6F8B9A2D3E4F5C6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2
```

### 复制机器码到剪贴板

```csharp
using System.Windows;

string machineCode = client.GetMachineCode();
Clipboard.SetText(machineCode);
MessageBox.Show($"机器码已复制到剪贴板:\n{machineCode}", "机器码");
```

### 机器码说明

- **生成规则**: 基于 CPU ID、主板序列号、硬盘序列号生成 MD5 哈希
- **唯一性**: 同一台机器的机器码始终相同
- **用途**: 用于生成预绑定的注册码，确保注册码只能在指定机器上使用

---

## 离线模式

### 启用离线缓存

SDK 支持离线验证，当服务器不可达时使用缓存的验证结果。

#### 配置离线模式

```json
{
  "EnableOfflineMode": true,
  "OfflineCacheHours": 24
}
```

#### 离线验证流程

```csharp
using var client = new LicenseClient("YourAppId");

// 首次联网验证（会缓存结果）
var result = await client.ValidateAsync("YOUR-KEY");

// 之后即使离线也能验证（24小时内）
// SDK 会自动尝试联网验证，失败时使用缓存
var offlineResult = await client.ValidateAsync("YOUR-KEY");
```

### 测试服务器连接

```csharp
using var client = new LicenseClient(
    baseUrl: "http://your-server:5232",
    appId: "YourAppId"
);

bool isConnected = await client.TestConnectionAsync();

if (isConnected)
{
    Console.WriteLine("✅ 服务器连接正常");
}
else
{
    Console.WriteLine("❌ 无法连接到服务器");
}
```

---

## 完整示例

### 示例 1：WPF 应用完整流程

```csharp
using System;
using System.Windows;
using RegisterSrv.ClientSDK;
using RegisterSrv.ClientSDK.Services;

namespace MyWpfApp
{
    public partial class App : Application
    {
        private const string APP_ID = "MyWpfApp";
        private const string APP_NAME = "我的WPF应用";
        private const string APP_VERSION = "1.0.0";
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 检查命令行参数
            bool skipLicense = e.Args.Length > 0 && 
                               e.Args[0] == "--skip-license";
            
            if (!skipLicense)
            {
                // 显示注册窗口
                if (!ShowLicenseWindow())
                {
                    // 验证失败，退出应用
                    Shutdown();
                    return;
                }
            }
            
            // 启动主窗口
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
        
        private bool ShowLicenseWindow()
        {
            var licenseWindow = new LicenseWindow(
                appId: APP_ID,
                appName: APP_NAME,
                appVersion: APP_VERSION
            );
            
            // 居中显示
            licenseWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // 模态显示
            bool? result = licenseWindow.ShowDialog();
            
            if (result == true && licenseWindow.IsLicenseValid)
            {
                // 保存已验证的注册码（可选）
                Properties.Settings.Default.LicenseKey = 
                    licenseWindow.ValidatedLicenseKey;
                Properties.Settings.Default.Save();
                
                return true;
            }
            
            return false;
        }
    }
}
```

### 示例 2：控制台应用

```csharp
using System;
using System.Threading.Tasks;
using RegisterSrv.ClientSDK.Services;

namespace MyConsoleApp
{
    class Program
    {
        private const string APP_ID = "MyConsoleApp";
        private const string SERVER_URL = "http://localhost:5232";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("         许可证验证系统");
            Console.WriteLine("========================================");
            Console.WriteLine();
            
            // 显示机器码
            using var client = new LicenseClient(SERVER_URL, APP_ID);
            string machineCode = client.GetMachineCode();
            Console.WriteLine($"您的机器码: {machineCode}");
            Console.WriteLine();
            
            // 输入注册码
            Console.Write("请输入注册码: ");
            string keyCode = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(keyCode))
            {
                Console.WriteLine("❌ 注册码不能为空！");
                return;
            }
            
            // 激活（首次使用）
            Console.WriteLine("\n正在激活...");
            var activationResult = await client.ActivateAsync(keyCode);
            
            if (!activationResult.IsSuccess)
            {
                Console.WriteLine($"⚠️  激活提示: {activationResult.Message}");
            }
            
            // 验证
            Console.WriteLine("正在验证...");
            var validateResult = await client.ValidateAsync(keyCode, "1.0.0");
            
            if (validateResult.IsValid)
            {
                Console.WriteLine("\n✅ 验证成功！");
                Console.WriteLine($"许可类型: {validateResult.LicenseType}");
                
                if (validateResult.RemainingDays.HasValue)
                {
                    Console.WriteLine($"剩余天数: {validateResult.RemainingDays.Value} 天");
                    
                    if (validateResult.IsExpiringSoon)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("⚠️  警告：许可证即将过期！");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.WriteLine("许可类型: 永久许可");
                }
                
                Console.WriteLine("\n按任意键启动应用程序...");
                Console.ReadKey();
                
                // 启动主程序逻辑
                RunApplication();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ 验证失败: {validateResult.Message}");
                Console.ResetColor();
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
        }
        
        static void RunApplication()
        {
            Console.Clear();
            Console.WriteLine("应用程序正在运行...");
            Console.WriteLine("按 Q 退出");
            
            while (Console.ReadKey(true).Key != ConsoleKey.Q)
            {
                // 应用程序主逻辑
            }
        }
    }
}
```

### 示例 3：后台服务验证

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RegisterSrv.ClientSDK.Services;

namespace MyWindowsService
{
    public class LicenseValidationService : BackgroundService
    {
        private readonly ILogger<LicenseValidationService> _logger;
        private const string APP_ID = "MyService";
        private const int CHECK_INTERVAL_HOURS = 24;
        
        public LicenseValidationService(ILogger<LicenseValidationService> logger)
        {
            _logger = logger;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 启动时验证
            if (!await ValidateLicenseAsync())
            {
                _logger.LogError("许可证验证失败，服务将停止");
                throw new InvalidOperationException("许可证验证失败");
            }
            
            // 定期验证
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromHours(CHECK_INTERVAL_HOURS), 
                        stoppingToken
                    );
                    
                    await ValidateLicenseAsync();
                }
                catch (TaskCanceledException)
                {
                    // 服务停止
                    break;
                }
            }
        }
        
        private async Task<bool> ValidateLicenseAsync()
        {
            try
            {
                using var client = new LicenseClient(APP_ID);
                
                // 从配置读取注册码
                string keyCode = Environment.GetEnvironmentVariable("LICENSE_KEY");
                
                if (string.IsNullOrEmpty(keyCode))
                {
                    _logger.LogError("未配置许可证密钥");
                    return false;
                }
                
                var result = await client.ValidateAsync(keyCode);
                
                if (result.IsValid)
                {
                    _logger.LogInformation("许可证验证成功");
                    
                    if (result.RemainingDays.HasValue)
                    {
                        _logger.LogInformation(
                            $"剩余天数: {result.RemainingDays.Value} 天"
                        );
                        
                        if (result.IsExpiringSoon)
                        {
                            _logger.LogWarning("许可证即将过期！");
                        }
                    }
                    
                    return true;
                }
                else
                {
                    _logger.LogError($"许可证验证失败: {result.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "许可证验证过程中发生错误");
                return false;
            }
        }
    }
}
```

---

## 最佳实践

### 1. 应用启动时验证 ✅

```csharp
// 在应用程序入口点验证
protected override void OnStartup(StartupEventArgs e)
{
    if (!ValidateLicense())
    {
        Shutdown();
        return;
    }
    
    // 继续启动
}
```

### 2. 定期验证（防止绕过） ✅

```csharp
// 每24小时验证一次
private async void StartPeriodicValidation()
{
    var timer = new System.Windows.Threading.DispatcherTimer
    {
        Interval = TimeSpan.FromHours(24)
    };
    
    timer.Tick += async (s, e) =>
    {
        var isValid = await RevalidateLicenseAsync();
        if (!isValid)
        {
            MessageBox.Show("许可证已失效，应用程序将退出");
            Application.Current.Shutdown();
        }
    };
    
    timer.Start();
}
```

### 3. 过期提醒 ✅

```csharp
private void CheckExpirationWarning(LicenseValidationResponse result)
{
    if (result.IsExpiringSoon && result.RemainingDays.HasValue)
    {
        MessageBox.Show(
            $"您的许可证将在 {result.RemainingDays.Value} 天后过期，" +
            "请及时续费！",
            "过期提醒",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
    }
}
```

### 4. 错误处理 ✅

```csharp
try
{
    var result = await client.ValidateAsync(keyCode);
    // 处理结果
}
catch (HttpRequestException ex)
{
    // 网络错误 - 可以使用离线缓存
    _logger.LogWarning($"网络连接失败: {ex.Message}");
    // 尝试离线验证...
}
catch (Exception ex)
{
    // 其他错误
    _logger.LogError(ex, "验证过程发生错误");
    throw;
}
```

### 5. 配置保存 ✅

```csharp
// 验证成功后保存配置
if (licenseWindow.IsLicenseValid)
{
    // 保存注册码（加密存储更安全）
    Properties.Settings.Default.LicenseKey = 
        licenseWindow.ValidatedLicenseKey;
    
    // 保存服务器地址
    Properties.Settings.Default.ServerUrl = serverUrl;
    
    Properties.Settings.Default.Save();
}
```

### 6. 安全建议 🔒

- ✅ 不要在代码中硬编码注册码
- ✅ 使用加密存储保存注册码
- ✅ 定期验证防止绕过
- ✅ 在关键功能前验证许可证
- ✅ 记录验证日志便于审计

---

## 常见问题

### Q1: 如何处理离线环境？

**A**: SDK 支持离线缓存模式：

```csharp
// 配置离线模式
{
  "EnableOfflineMode": true,
  "OfflineCacheHours": 24  // 缓存有效期
}

// 首次联网验证成功后，24小时内离线也能验证
```

### Q2: 验证失败怎么办？

**A**: 检查以下几点：

1. 服务器地址是否正确
2. 注册码是否正确
3. 注册码是否已过期
4. 机器码是否匹配（预绑定注册码）
5. 网络连接是否正常

```csharp
// 测试服务器连接
bool isConnected = await client.TestConnectionAsync();

// 查看详细错误信息
Console.WriteLine($"错误: {result.Message}");
```

### Q3: 如何更新服务器地址？

**A**: 三种方式：

```csharp
// 方式1: 修改配置文件
// registersrv.json 中修改 ServerUrl

// 方式2: 代码更新
ClientConfig.Instance.UpdateServerUrl("http://new-server:5232");

// 方式3: 创建客户端时指定
using var client = new LicenseClient("http://new-server:5232", "YourAppId");
```

### Q4: 剩余天数不显示？

**A**: 确保使用 **v1.0.1 或更高版本**：

```xml
<PackageReference Include="RegisterSrv.ClientSDK" Version="1.0.1" />
```

检查代码：
```csharp
if (result.RemainingDays.HasValue)
{
    Console.WriteLine($"剩余: {result.RemainingDays.Value} 天");
}
```

### Q5: 如何在多个项目中使用？

**A**: 每个项目独立配置：

```csharp
// 项目A
using var clientA = new LicenseClient("ProjectA");

// 项目B
using var clientB = new LicenseClient("ProjectB");
```

---

## API 参考

### LicenseClient 类

```csharp
namespace RegisterSrv.ClientSDK.Services;

public class LicenseClient : IDisposable
{
    // 构造函数
    public LicenseClient(string appId);
    public LicenseClient(string baseUrl, string appId);
    
    // 验证注册码
    public Task<LicenseValidationResponse> ValidateAsync(
        string keyCode, 
        string? appVersion = null
    );
    
    // 激活注册码
    public Task<LicenseActivationResponse> ActivateAsync(string keyCode);
    
    // 获取机器码
    public string GetMachineCode();
    
    // 测试连接
    public Task<bool> TestConnectionAsync();
    
    // 释放资源
    public void Dispose();
}
```

### LicenseValidationResponse 类

```csharp
public class LicenseValidationResponse
{
    // 是否验证成功
    public bool IsValid { get; set; }
    
    // 消息
    public string Message { get; set; }
    
    // 剩余天数（v1.0.1 新增）
    public int? RemainingDays { get; set; }
    
    // 是否即将过期（v1.0.1 新增）
    public bool IsExpiringSoon { get; set; }
    
    // 许可证类型
    public string? LicenseType { get; set; }
    
    // 客户信息
    public string? CustomerInfo { get; set; }
}
```

### LicenseWindow 类

```csharp
namespace RegisterSrv.ClientSDK;

public partial class LicenseWindow : Window
{
    // 构造函数
    public LicenseWindow(
        string appId,
        string appName,
        string appVersion,
        string? serverUrl = null
    );
    
    // 属性
    public bool IsLicenseValid { get; }
    public string? ValidatedLicenseKey { get; }
}
```

### ClientConfig 类

```csharp
namespace RegisterSrv.ClientSDK.Config;

public class ClientConfig
{
    // 单例实例
    public static ClientConfig Instance { get; }
    
    // 属性
    public string ServerUrl { get; }
    public int TimeoutSeconds { get; }
    public bool EnableOfflineMode { get; }
    public int OfflineCacheHours { get; }
    public int RetryCount { get; }
    
    // 方法
    public void UpdateServerUrl(string serverUrl);
    public static void CreateDefaultConfigFile(string? customServerUrl = null);
}
```

---

## 更新历史

### v1.0.1 (2025-10-21) - 当前版本

**新增**:
- ✨ `RemainingDays` 属性 - 获取许可证剩余天数
- ✨ `IsExpiringSoon` 属性 - 检查是否即将过期（≤7天）
- ✨ LicenseWindow 自动显示剩余天数

**修复**:
- 🐛 修复剩余天数数据丢失问题
- 🐛 修复 JSON 反序列化嵌套结构处理

**详细**: 参见 `CHANGELOG.md`

### v1.0.0

- 🎉 首次发布
- ✨ 许可证验证功能
- ✨ 机器码生成
- ✨ 注册窗口 UI
- ✨ 配置管理

---

## 技术支持

### 文档资源
- 📚 使用教程: 本文档
- 📋 发布说明: `RegisterSrv.ClientSDK v1.0.1 发布指南.md`
- 🔧 修复指南: `RegisterSrv.ClientSDK 剩余天数显示问题修复指南.md`
- 📝 更新日志: `CHANGELOG.md`

### 联系方式
- 📧 邮箱: support@registersrv.com
- 🐛 问题反馈: GitHub Issues
- 💬 讨论: GitHub Discussions

---

**最后更新**: 2025-10-21  
**文档版本**: v1.0.1  
**作者**: RegisterSrv Team

---

🎉 **恭喜！您已掌握 RegisterSrv.ClientSDK 的使用方法！**

如有问题，请参考常见问题部分或联系技术支持。

