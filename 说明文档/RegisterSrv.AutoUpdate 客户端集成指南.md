# RegisterSrv.AutoUpdate 客户端集成指南

## 📦 简介

`RegisterSrv.AutoUpdate` 是一个功能完整的 WPF 应用程序自动更新组件，提供版本检查、自动下载、安装更新等功能。支持在线升级、强制更新、进度显示、关于窗口等特性。

---

## 🚀 快速开始

### 1. 安装 NuGet 包

#### 方式一：通过 Package Manager Console
```powershell
Install-Package RegisterSrv.AutoUpdate
```

#### 方式二：通过 .NET CLI
```bash
dotnet add package RegisterSrv.AutoUpdate
```

#### 方式三：手动安装（本地测试）
将生成的 `RegisterSrv.AutoUpdate.1.0.0.nupkg` 文件复制到本地 NuGet 源，然后在项目中引用。

---

## 📋 基础配置

### 1. 创建更新配置

在应用程序启动时配置更新信息：

```csharp
using RegisterSrv.AutoUpdate;

// 创建更新配置
var updateConfig = new UpdateConfig
{
    ServerUrl = "http://localhost:5000",    // 更新服务器地址
    AppId = "MyApp",                        // 应用程序 ID（与服务器注册的应用ID一致）
    AppName = "我的应用程序",                // 应用程序显示名称
    CurrentVersion = "1.0.0",               // 当前版本号
    AutoCheckOnStartup = true,              // 启动时自动检查更新
    SilentUpdate = false                    // 是否静默更新（false表示显示UI）
};
```

### 2. 初始化更新管理器

```csharp
var updateManager = new UpdateManager(updateConfig);
```

---

## 💡 使用场景

### 场景一：启动时自动检查更新（推荐）

在应用程序 `App.xaml.cs` 或 `MainWindow` 构造函数中：

```csharp
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var updateConfig = new UpdateConfig
        {
            ServerUrl = "http://your-server.com",
            AppId = "YourAppId",
            AppName = "Your App Name",
            CurrentVersion = "1.0.0"
        };
        
        var updateManager = new UpdateManager(updateConfig);
        
        // 静默检查更新（有更新时显示对话框）
        await updateManager.CheckAndUpdateAsync(silent: true);
    }
}
```

### 场景二：手动检查更新（菜单项）

在 WPF 窗口中添加"检查更新"菜单：

```xaml
<MenuItem Header="帮助(_H)">
    <MenuItem Header="检查更新(_U)" Click="MenuCheckUpdate_Click"/>
    <MenuItem Header="关于(_A)" Click="MenuAbout_Click"/>
</MenuItem>
```

```csharp
private UpdateManager? _updateManager;

public MainWindow()
{
    InitializeComponent();
    
    // 初始化更新管理器
    _updateManager = new UpdateManager(new UpdateConfig
    {
        ServerUrl = "http://your-server.com",
        AppId = "YourAppId",
        AppName = "Your App Name",
        CurrentVersion = "1.0.0"
    });
}

private async void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
{
    // 完整的更新流程（带UI提示）
    await _updateManager.CheckAndUpdateAsync(this, silent: false);
}

private void MenuAbout_Click(object sender, RoutedEventArgs e)
{
    // 显示关于窗口（包含版本信息和检查更新按钮）
    _updateManager.ShowAboutWindow(this);
}
```

### 场景三：自定义更新流程

如果需要更细粒度的控制：

```csharp
private async Task CustomUpdateAsync()
{
    var updateManager = new UpdateManager(updateConfig);
    
    // 1. 检查更新（静默）
    var checkResult = await updateManager.CheckUpdateSilentAsync();
    
    if (checkResult.IsSuccess && checkResult.HasUpdate)
    {
        var updateInfo = checkResult.UpdateInfo!;
        
        // 2. 显示自定义更新确认对话框
        if (MessageBox.Show(
            $"发现新版本 {updateInfo.Version}，是否立即更新？",
            "更新提示",
            MessageBoxButton.YesNo) == MessageBoxResult.Yes)
        {
            // 3. 下载并安装（显示进度）
            var installResult = await updateManager.DownloadAndInstallAsync(updateInfo, showProgress: true);
            
            if (installResult.IsSuccess)
            {
                // 4. 重启应用
                updateManager.RestartApplication(delaySeconds: 2);
            }
        }
    }
}
```

### 场景四：静默更新

适用于后台服务或需要无用户干预的场景：

```csharp
private async Task SilentUpdateAsync()
{
    var updateConfig = new UpdateConfig
    {
        ServerUrl = "http://your-server.com",
        AppId = "YourAppId",
        AppName = "Your App Name",
        CurrentVersion = "1.0.0",
        SilentUpdate = true  // 启用静默模式
    };
    
    var updateClient = new UpdateClient(
        updateConfig.ServerUrl, 
        updateConfig.AppId
    );
    
    // 检查更新
    var checkResult = await updateClient.CheckUpdateAsync(updateConfig.CurrentVersion);
    
    if (checkResult.IsSuccess && checkResult.HasUpdate)
    {
        // 下载并安装（不显示UI）
        var installResult = await updateClient.DownloadAndInstallAsync(
            checkResult.UpdateInfo!
        );
        
        if (installResult.IsSuccess)
        {
            // 重启
            updateClient.RestartApplication();
        }
    }
}
```

---

## 🎨 UI 组件说明

### 1. UpdateDialog - 更新确认对话框

显示新版本信息，让用户选择是否更新。

**特性**：
- ✅ 显示版本对比（当前版本 → 新版本）
- ✅ 显示更新说明
- ✅ 显示文件大小、发布时间
- ✅ 支持强制更新（禁用"稍后提醒"按钮）
- ✅ 现代化 UI 设计

### 2. UpdateProgressWindow - 更新进度窗口

显示下载和安装进度。

**特性**：
- ✅ 实时进度条
- ✅ 状态消息显示
- ✅ 无边框透明窗口设计
- ✅ 自动关闭

### 3. AboutWindow - 关于窗口

显示应用程序信息和检查更新功能。

**特性**：
- ✅ 显示应用名称、版本、版权信息
- ✅ 显示系统信息
- ✅ 自动检查服务器版本
- ✅ 一键更新按钮
- ✅ 现代化 UI 设计

---

## 🔧 高级功能

### 1. 事件订阅

监听更新过程中的事件：

```csharp
var updateManager = new UpdateManager(updateConfig);

// 订阅进度变化事件
updateManager.ProgressChanged += (sender, e) =>
{
    Console.WriteLine($"进度: {e.Progress}% - {e.Message}");
};

// 订阅状态变化事件
updateManager.StatusChanged += (sender, status) =>
{
    Console.WriteLine($"状态: {status}");
};
```

### 2. 版本号比较

使用标准的语义化版本号（SemVer）：

```
1.0.0    - 主版本号.次版本号.补丁号
1.2.3    - 正式版本
2.0.0    - 重大更新
```

### 3. 强制更新

在服务器端将版本标记为"强制更新"后：
- 用户无法选择"稍后提醒"
- 显示红色强制更新标记
- 必须完成更新才能继续使用

### 4. 文件校验

所有下载的更新包会自动进行 MD5 校验，确保文件完整性和安全性。

---

## 📝 完整示例

### 示例：完整的 WPF 应用程序集成

#### App.xaml.cs

```csharp
using System.Windows;
using RegisterSrv.AutoUpdate;

namespace MyWpfApp
{
    public partial class App : Application
    {
        public static UpdateConfig UpdateConfig { get; private set; } = null!;
        public static UpdateManager UpdateManager { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化更新配置
            UpdateConfig = new UpdateConfig
            {
                ServerUrl = "http://localhost:5000",
                AppId = "MyWpfApp",
                AppName = "我的 WPF 应用",
                CurrentVersion = "1.0.0",
                AutoCheckOnStartup = true
            };

            // 初始化更新管理器
            UpdateManager = new UpdateManager(UpdateConfig);

            // 启动时检查更新（静默）
            if (UpdateConfig.AutoCheckOnStartup)
            {
                await UpdateManager.CheckAndUpdateAsync(silent: true);
            }
        }
    }
}
```

#### MainWindow.xaml

```xaml
<Window x:Class="MyWpfApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="我的应用程序" Height="450" Width="800">
    <DockPanel>
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="文件(_F)">
                <MenuItem Header="退出(_X)" Click="MenuExit_Click"/>
            </MenuItem>
            <MenuItem Header="帮助(_H)">
                <MenuItem Header="检查更新(_U)" Click="MenuCheckUpdate_Click"/>
                <Separator/>
                <MenuItem Header="关于(_A)" Click="MenuAbout_Click"/>
            </MenuItem>
        </Menu>
        
        <Grid>
            <!-- 应用程序主内容 -->
            <TextBlock Text="应用程序主界面" 
                       HorizontalAlignment="Center" 
                       VerticalAlignment="Center"
                       FontSize="24"/>
        </Grid>
    </DockPanel>
</Window>
```

#### MainWindow.xaml.cs

```csharp
using System.Windows;

namespace MyWpfApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // 设置标题显示版本号
            Title = $"{App.UpdateConfig.AppName} v{App.UpdateConfig.CurrentVersion}";
        }

        private async void MenuCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            await App.UpdateManager.CheckAndUpdateAsync(this, silent: false);
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            App.UpdateManager.ShowAboutWindow(this);
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
```

---

## ⚙️ 配置参考

### UpdateConfig 配置项

| 属性 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `ServerUrl` | `string` | ✅ | 更新服务器地址（如：`http://update.myapp.com`） |
| `AppId` | `string` | ✅ | 应用程序 ID，必须与服务器注册的一致 |
| `AppName` | `string` | ✅ | 应用程序显示名称 |
| `CurrentVersion` | `string` | ✅ | 当前应用程序版本号 |
| `AutoCheckOnStartup` | `bool` | ❌ | 启动时自动检查更新（默认：`true`） |
| `SilentUpdate` | `bool` | ❌ | 静默更新，不显示 UI（默认：`false`） |

---

## 🔍 API 参考

### UpdateManager 类

#### 构造函数
```csharp
public UpdateManager(UpdateConfig config)
```

#### 主要方法

| 方法 | 说明 |
|------|------|
| `CheckUpdateWithUIAsync()` | 检查更新（带 UI 提示） |
| `CheckUpdateSilentAsync()` | 检查更新（静默） |
| `ShowUpdateDialog(UpdateInfo, Window?)` | 显示更新确认对话框 |
| `DownloadAndInstallAsync(UpdateInfo, bool)` | 下载并安装更新 |
| `CheckAndUpdateAsync(Window?, bool)` | 完整更新流程（推荐） |
| `RestartApplication(int)` | 重启应用程序 |
| `ShowAboutWindow(Window?)` | 显示关于窗口 |

### UpdateClient 类

底层更新客户端（如需更细粒度控制可直接使用）。

#### 构造函数
```csharp
public UpdateClient(string baseUrl, string appId, HttpClient? httpClient = null)
```

#### 主要方法

| 方法 | 说明 |
|------|------|
| `CheckUpdateAsync(string)` | 检查更新 |
| `DownloadAndInstallAsync(UpdateInfo, string?)` | 下载并安装 |
| `DownloadUpdateAsync(UpdateInfo, string)` | 仅下载更新包 |
| `RestartApplication(string?, string?, int)` | 重启应用程序 |

---

## 🛡️ 最佳实践

### 1. 版本号管理

建议使用 AssemblyInfo 或项目属性统一管理版本号：

```csharp
// 从程序集获取版本号
var version = System.Reflection.Assembly.GetExecutingAssembly()
    .GetName().Version?.ToString(3) ?? "1.0.0";

var updateConfig = new UpdateConfig
{
    CurrentVersion = version,
    // ... 其他配置
};
```

### 2. 异常处理

始终包含异常处理以提高健壮性：

```csharp
try
{
    await updateManager.CheckAndUpdateAsync(this);
}
catch (HttpRequestException ex)
{
    MessageBox.Show($"网络连接失败：{ex.Message}", "错误", 
        MessageBoxButton.OK, MessageBoxImage.Error);
}
catch (Exception ex)
{
    MessageBox.Show($"更新检查失败：{ex.Message}", "错误", 
        MessageBoxButton.OK, MessageBoxImage.Error);
}
```

### 3. 配置外部化

将配置存储在 `appsettings.json` 中：

```json
{
  "UpdateSettings": {
    "ServerUrl": "http://update.myapp.com",
    "AppId": "MyApp",
    "AutoCheckOnStartup": true
  }
}
```

```csharp
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var updateConfig = new UpdateConfig
{
    ServerUrl = configuration["UpdateSettings:ServerUrl"]!,
    AppId = configuration["UpdateSettings:AppId"]!,
    // ...
};
```

### 4. 用户体验优化

- ✅ 启动时静默检查，有更新时才提示
- ✅ 提供"稍后提醒"选项（非强制更新）
- ✅ 显示详细的更新说明
- ✅ 在关于窗口中提供手动检查入口
- ✅ 更新完成后提示重启

---

## ❓ 常见问题

### Q1: 如何测试更新功能？

**A:** 
1. 启动 RegisterSrv.Server 服务器
2. 在版本管理中上传新版本
3. 将客户端的 `CurrentVersion` 设置为较低版本
4. 运行客户端测试

### Q2: 强制更新如何实现？

**A:** 在服务器端添加版本时勾选"强制更新"选项，客户端会自动禁用"稍后提醒"按钮。

### Q3: 如何自定义 UI？

**A:** 可以继承或修改 `UpdateDialog`、`UpdateProgressWindow`、`AboutWindow` 类，自定义 XAML 界面。

### Q4: 支持哪些 .NET 版本？

**A:** 目前支持 .NET 8.0-windows 及以上版本，仅限 WPF 应用程序。

### Q5: 更新包格式要求？

**A:** 更新包必须是 ZIP 格式，解压后的文件结构应与应用程序目录结构一致。

---

## 📞 技术支持

如有问题或建议，请联系技术支持团队。

**项目地址**: https://github.com/registerSrv/RegisterSrv

---

## 📄 许可证

本组件采用 MIT 许可证发布。

---

**最后更新**: 2024-01-01  
**版本**: 1.0.0 