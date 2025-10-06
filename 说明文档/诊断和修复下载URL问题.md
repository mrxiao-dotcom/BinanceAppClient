# 诊断和修复下载 URL 问题

## 问题分析

错误信息：`An invalid request URI was provided. Either the request URI must be an absolute URI or BaseAddress must be set`

这个错误发生在**下载更新包**时，说明：
- ✅ 服务器连接正常
- ✅ 版本检查成功
- ❌ 服务器返回的下载 URL 格式不正确（可能是相对路径）

## 原因

RegisterSrv 服务器可能返回的下载 URL 是相对路径（如 `/api/updates/download/xxx`），而不是完整的绝对路径（如 `http://192.168.1.101:8080/api/updates/download/xxx`）。

HttpClient 需要完整的绝对 URL 才能下载文件。

## 解决方案

### 方案 1：修复服务器端配置（推荐）

联系服务器管理员，确保 API 返回完整的绝对 URL。

**服务器端需要返回的 JSON 格式**：
```json
{
  "version": "1.0.2",
  "downloadUrl": "http://192.168.1.101:8080/api/updates/download/xxx.zip",
  ...
}
```

而不是：
```json
{
  "version": "1.0.2",
  "downloadUrl": "/api/updates/download/xxx.zip",
  ...
}
```

### 方案 2：在客户端添加兼容处理

如果无法修改服务器端，我们可以在客户端代码中添加 URL 修正逻辑。

#### 步骤 1：创建自定义的 UpdateClient 包装类

创建新文件 `src/BinanceApps.WPF/FixedUpdateManager.cs`：

```csharp
using System;
using System.Threading.Tasks;
using RegisterSrv.AutoUpdate;

namespace BinanceApps.WPF
{
    /// <summary>
    /// 修复下载 URL 问题的自定义更新管理器
    /// </summary>
    public class FixedUpdateManager
    {
        private readonly UpdateManager _inner;
        private readonly string _serverUrl;

        public FixedUpdateManager(UpdateConfig config)
        {
            _inner = new UpdateManager(config);
            _serverUrl = config.ServerUrl.TrimEnd('/');
        }

        public async Task<bool> CheckAndUpdateAsync(System.Windows.Window? owner = null, bool silent = false)
        {
            try
            {
                Console.WriteLine($"🔍 开始检查更新...");
                
                // 检查更新
                var checkResult = await _inner.CheckUpdateSilentAsync();
                
                if (!checkResult.IsSuccess)
                {
                    Console.WriteLine($"❌ 检查更新失败: {checkResult.ErrorMessage}");
                    return false;
                }
                
                if (!checkResult.HasUpdate)
                {
                    Console.WriteLine($"✅ 已是最新版本");
                    if (!silent)
                    {
                        System.Windows.MessageBox.Show("当前已是最新版本", "检查更新", 
                            System.Windows.MessageBoxButton.OK, 
                            System.Windows.MessageBoxImage.Information);
                    }
                    return false;
                }
                
                var updateInfo = checkResult.UpdateInfo!;
                Console.WriteLine($"📦 发现新版本: {updateInfo.Version}");
                Console.WriteLine($"📥 原始下载 URL: {updateInfo.DownloadUrl}");
                
                // 修正下载 URL（如果是相对路径）
                if (!Uri.IsWellFormedUriString(updateInfo.DownloadUrl, UriKind.Absolute))
                {
                    var fixedUrl = $"{_serverUrl}{(updateInfo.DownloadUrl.StartsWith("/") ? "" : "/")}{updateInfo.DownloadUrl}";
                    Console.WriteLine($"🔧 修正后 URL: {fixedUrl}");
                    
                    // 创建新的 UpdateInfo 对象（使用反射或重新构造）
                    // 注意：这里需要根据 UpdateInfo 的实际结构来调整
                    updateInfo = new UpdateInfo
                    {
                        Version = updateInfo.Version,
                        DownloadUrl = fixedUrl,
                        ReleaseNotes = updateInfo.ReleaseNotes,
                        FileSize = updateInfo.FileSize,
                        FileMD5 = updateInfo.FileMD5,
                        IsForceUpdate = updateInfo.IsForceUpdate,
                        PublishedAt = updateInfo.PublishedAt
                    };
                }
                
                // 显示更新对话框
                if (!silent || updateInfo.IsForceUpdate)
                {
                    var dialogResult = _inner.ShowUpdateDialog(updateInfo, owner);
                    if (!dialogResult)
                    {
                        Console.WriteLine($"⏭️  用户选择稍后更新");
                        return false;
                    }
                }
                
                // 下载并安装
                Console.WriteLine($"⬇️  开始下载更新...");
                var installResult = await _inner.DownloadAndInstallAsync(updateInfo, showProgress: true);
                
                if (installResult.IsSuccess)
                {
                    Console.WriteLine($"✅ 更新安装成功");
                    // 提示重启
                    var restart = System.Windows.MessageBox.Show(
                        "更新已安装，需要重启应用程序。是否立即重启？",
                        "更新成功",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question
                    );
                    
                    if (restart == System.Windows.MessageBoxResult.Yes)
                    {
                        _inner.RestartApplication();
                    }
                    return true;
                }
                else
                {
                    Console.WriteLine($"❌ 更新安装失败: {installResult.ErrorMessage}");
                    System.Windows.MessageBox.Show($"更新失败：{installResult.ErrorMessage}", 
                        "错误", 
                        System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 更新过程异常: {ex.Message}");
                Console.WriteLine($"   堆栈: {ex.StackTrace}");
                throw;
            }
        }

        public void ShowAboutWindow(System.Windows.Window? owner = null)
        {
            _inner.ShowAboutWindow(owner);
        }
    }
}
```

#### 步骤 2：修改 App.xaml.cs

将 `UpdateManager` 替换为 `FixedUpdateManager`：

```csharp
// 修改这一行
public static FixedUpdateManager? UpdateManager { get; private set; }

// 修改初始化代码
UpdateManager = new FixedUpdateManager(updateConfig);
```

#### 步骤 3：重新编译和测试

1. 删除 bin 和 obj 文件夹
2. 重新生成解决方案
3. 运行应用程序
4. 测试更新功能

### 方案 3：临时workaround - 手动测试 API

使用 Postman 或浏览器测试服务器 API：

```
GET http://192.168.1.101:8080/api/updates/check?appId=App_20250928132921&version=1.0.1
```

查看返回的 JSON，特别是 `downloadUrl` 字段的值。

**如果返回的是相对路径**，说明需要使用方案 2 或联系服务器管理员修复。

## 🔍 调试步骤

### 1. 添加更详细的日志

在 `MenuItem_CheckUpdate_Click` 方法中添加异常捕获：

```csharp
private async void MenuItem_CheckUpdate_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (App.UpdateManager != null)
        {
            Console.WriteLine("🔍 [调试] 开始手动检查更新");
            await App.UpdateManager.CheckAndUpdateAsync(this, silent: false);
            Console.WriteLine("✅ [调试] 更新检查完成");
        }
        else
        {
            MessageBox.Show("更新管理器未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ [调试] 更新失败异常:");
        Console.WriteLine($"   消息: {ex.Message}");
        Console.WriteLine($"   类型: {ex.GetType().Name}");
        Console.WriteLine($"   堆栈: {ex.StackTrace}");
        
        // 如果有内部异常，也打印出来
        if (ex.InnerException != null)
        {
            Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
            Console.WriteLine($"   内部堆栈: {ex.InnerException.StackTrace}");
        }
        
        MessageBox.Show($"检查更新失败：{ex.Message}\n\n详细信息请查看控制台输出", 
            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

### 2. 运行并查看输出

重新运行应用，点击"检查更新"，查看控制台输出，特别关注：
- 原始下载 URL 是什么格式
- 是否是相对路径

## 🎯 推荐做法

**优先级顺序**：
1. **首选**：联系服务器管理员，修改 API 返回完整的绝对 URL
2. **次选**：使用方案 2 创建 FixedUpdateManager 类
3. **临时**：添加更详细的日志，收集更多信息后再决定

---

**如果需要，我可以帮您实现方案 2 的代码。** 