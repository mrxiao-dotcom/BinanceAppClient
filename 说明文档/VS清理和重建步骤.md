# Visual Studio 清理和重建步骤

## 问题原因

Visual Studio 可能缓存了旧的配置文件，导致修改后的 App.config 没有被使用。

## 解决步骤

### 步骤 1：清理解决方案

在 Visual Studio 中：
1. 点击菜单：**生成** → **清理解决方案**
2. 等待清理完成

### 步骤 2：手动删除 bin 和 obj 目录

1. 关闭 Visual Studio
2. 打开文件资源管理器
3. 导航到：`D:\CSharpProjects\BinanceAppsClient\src\BinanceApps.WPF`
4. **删除** `bin` 文件夹
5. **删除** `obj` 文件夹
6. **删除** `publish` 文件夹（如果存在）

### 步骤 3：验证 App.config 是否正确

1. 打开 `D:\CSharpProjects\BinanceAppsClient\src\BinanceApps.WPF\App.config`
2. 检查第 10 行，确认为：
   ```xml
   <add key="LicenseServerUrl" value="http://192.168.1.101:8080" />
   ```
3. **确保没有重复的 `http://`**
4. **确保末尾没有 `/`**

### 步骤 4：重新打开 Visual Studio

1. 启动 Visual Studio
2. 打开解决方案
3. 等待 NuGet 包还原完成

### 步骤 5：重新生成解决方案

1. 点击菜单：**生成** → **重新生成解决方案**
2. 检查输出窗口，确认没有错误
3. 确认 "生成成功" 消息

### 步骤 6：验证配置文件已复制

1. 打开文件资源管理器
2. 导航到：`D:\CSharpProjects\BinanceAppsClient\src\BinanceApps.WPF\bin\Debug\net9.0-windows`
3. 找到 `App.config` 或 `BinanceApps.WPF.dll.config`
4. 打开该文件，确认服务器地址正确：
   ```xml
   <add key="LicenseServerUrl" value="http://192.168.1.101:8080" />
   ```

### 步骤 7：运行应用程序

1. 在 Visual Studio 中按 F5 或点击"启动"
2. 观察控制台输出（如果有）
3. 应该看到：
   ```
   🌐 服务器地址: http://192.168.1.101:8080
   ✅ 自动更新管理器已初始化 (版本: 1.0.1)
   ```

### 步骤 8：测试更新功能

1. 应用启动后，点击菜单：**帮助** → **检查更新**
2. 如果仍然出现错误，请记录完整的错误消息

## 🔍 如果问题仍然存在

### 检查点 1：确认服务器地址

打开 Visual Studio 的 **"输出"** 窗口，查找：
```
🌐 服务器地址: http://192.168.1.101:8080
```

如果显示的是其他地址（如 `http://http://...`），说明配置文件没有正确更新。

### 检查点 2：确认更新服务器运行

1. 打开浏览器
2. 访问：`http://192.168.1.101:8080`
3. 确认服务器正常响应

### 检查点 3：查看详细错误

如果还是报错，请提供：
1. 完整的错误消息
2. Visual Studio 输出窗口的内容
3. 应用程序控制台输出（如果有）

## 🛠️ 备用方案：使用命令行编译

如果 VS 一直有问题，可以使用命令行：

```cmd
cd D:\CSharpProjects\BinanceAppsClient\src\BinanceApps.WPF
dotnet clean
rmdir /s /q bin
rmdir /s /q obj
dotnet build -c Debug
dotnet run -c Debug
```

## ⚙️ 高级诊断

### 在代码中添加调试输出

在 `App.xaml.cs` 的 OnStartup 方法中，添加更多日志：

```csharp
// 在第 42 行之后添加
var updateConfig = new UpdateConfig
{
    ServerUrl = "http://192.168.1.101:8080",
    AppId = appId,
    AppName = appName,
    CurrentVersion = GetApplicationVersion(),
    AutoCheckOnStartup = true,
    SilentUpdate = false
};

// 添加这行调试输出
Console.WriteLine($"🔧 更新服务器 URL: {updateConfig.ServerUrl}");
Console.WriteLine($"🔧 应用 ID: {updateConfig.AppId}");
Console.WriteLine($"🔧 当前版本: {updateConfig.CurrentVersion}");
```

重新编译运行，查看输出的 URL 是否正确。

---

**如果按照上述步骤仍然有问题，请提供详细的错误信息和日志输出。** 