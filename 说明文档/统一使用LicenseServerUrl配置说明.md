# 统一使用 LicenseServerUrl 配置说明

## 📋 修改说明

### 问题背景

**原问题**：
- 日志显示合约API使用的是 `localhost:8080`
- 但配置文件中有正确的服务器地址
- 运行的是已部署到 `D:\Apps\SearchCoins\` 的版本
- 该版本可能是旧代码编译的

### 用户需求

统一使用 `App.config` 中的 `LicenseServerUrl`，而不需要单独配置 `ContractApiServerUrl`。

## ✅ 解决方案

### 代码修改

**文件**：`src/BinanceApps.WPF/MainWindow.xaml.cs`  
**修改内容**：

```csharp
// 优先读取 ContractApiServerUrl，如果不存在则使用 LicenseServerUrl
var contractApiUrl = ConfigurationManager.AppSettings["ContractApiServerUrl"];
var licenseServerUrl = ConfigurationManager.AppSettings["LicenseServerUrl"];

// 如果 ContractApiServerUrl 未配置，使用 LicenseServerUrl
if (string.IsNullOrWhiteSpace(contractApiUrl))
{
    contractApiUrl = licenseServerUrl;
    Console.WriteLine($"🔍 ContractApiServerUrl 未配置，使用 LicenseServerUrl: {contractApiUrl}");
}
else
{
    Console.WriteLine($"🔍 使用 ContractApiServerUrl: {contractApiUrl}");
}

// 如果两者都未配置，使用默认值
if (string.IsNullOrWhiteSpace(contractApiUrl))
{
    contractApiUrl = "http://localhost:8080";
    Console.WriteLine($"⚠️ 两者都未配置，使用默认值: {contractApiUrl}");
}

Console.WriteLine($"✅ 合约API最终地址: {contractApiUrl}");
```

### 配置读取优先级

1. **优先级1**：`ContractApiServerUrl`（如果配置了）
2. **优先级2**：`LicenseServerUrl`（如果 ContractApiServerUrl 未配置）
3. **优先级3**：默认值 `http://localhost:8080`（如果两者都未配置）

### 好处

✅ **向后兼容**：如果配置了 `ContractApiServerUrl`，仍然优先使用  
✅ **统一管理**：如果未配置 `ContractApiServerUrl`，自动使用 `LicenseServerUrl`  
✅ **简化配置**：大多数情况下只需配置一个 `LicenseServerUrl` 即可  
✅ **详细日志**：清楚显示使用的是哪个配置源

## 📄 配置文件示例

### 最简配置（推荐）

只需配置 `LicenseServerUrl`：

```xml
<appSettings>
    <add key="LicenseServerUrl" value="http://38.181.35.75:8080" />
    <!-- ContractApiServerUrl 可以不配置，会自动使用 LicenseServerUrl -->
</appSettings>
```

**启动日志**：
```
🔍 ContractApiServerUrl 未配置，使用 LicenseServerUrl: http://38.181.35.75:8080
✅ 合约API最终地址: http://38.181.35.75:8080
🌐 API地址: http://38.181.35.75:8080/api/contract
```

### 完整配置（如果服务器地址不同）

如果许可证服务器和合约API服务器地址不同：

```xml
<appSettings>
    <add key="LicenseServerUrl" value="http://38.181.35.75:8080" />
    <add key="ContractApiServerUrl" value="http://另一个服务器地址:8080" />
</appSettings>
```

**启动日志**：
```
🔍 使用 ContractApiServerUrl: http://另一个服务器地址:8080
✅ 合约API最终地址: http://另一个服务器地址:8080
🌐 API地址: http://另一个服务器地址:8080/api/contract
```

## 🚀 部署步骤

### 方法1：使用自动部署脚本（推荐）

运行：
```cmd
发布并部署到SearchCoins.cmd
```

该脚本会：
1. ✅ 编译最新 Release 版本
2. ✅ 发布到临时目录
3. ✅ 备份旧版本到带时间戳的目录
4. ✅ 部署新版本到 `D:\Apps\SearchCoins\`
5. ✅ **保护配置文件**（不会被覆盖）
6. ✅ **保护数据文件**（.db, .log, .dat）
7. ✅ 验证配置文件
8. ✅ 清理临时文件

### 方法2：手动部署

```powershell
# 1. 编译
dotnet build src/BinanceApps.WPF/BinanceApps.WPF.csproj -c Release

# 2. 发布
dotnet publish src/BinanceApps.WPF/BinanceApps.WPF.csproj -c Release -o publish_temp

# 3. 关闭正在运行的程序

# 4. 备份（可选）
Copy-Item -Path "D:\Apps\SearchCoins" -Destination "D:\Apps\SearchCoins_backup" -Recurse

# 5. 复制新文件（不覆盖配置）
Copy-Item -Path "publish_temp\*" -Destination "D:\Apps\SearchCoins\" -Recurse -Force -Exclude "*.db","*.log","*.dat","*.config"

# 6. 清理
Remove-Item -Recurse -Force publish_temp
```

### 方法3：仅更新配置文件（临时方案）

如果暂时无法重新部署，可以手动修改配置文件：

1. 打开 `D:\Apps\SearchCoins\BinanceApps.WPF.dll.config`
2. 确保有以下配置：
   ```xml
   <add key="LicenseServerUrl" value="http://38.181.35.75:8080" />
   ```
3. 删除或注释掉 `ContractApiServerUrl`（如果存在且值不对）
4. 保存文件
5. 重启程序

**⚠️ 注意**：方法3只是临时解决，推荐使用方法1重新部署。

## 🔍 验证步骤

### 步骤1：检查配置文件

```powershell
# 查看 LicenseServerUrl 配置
type "D:\Apps\SearchCoins\BinanceApps.WPF.dll.config" | findstr LicenseServerUrl

# 查看 ContractApiServerUrl 配置（可能不存在）
type "D:\Apps\SearchCoins\BinanceApps.WPF.dll.config" | findstr ContractApiServerUrl
```

### 步骤2：运行程序查看启动日志

**正确的日志应该是**：

```
🔍 ContractApiServerUrl 未配置，使用 LicenseServerUrl: http://38.181.35.75:8080
✅ 合约API最终地址: http://38.181.35.75:8080
📊 开始从API加载合约流通量信息...
🌐 API地址: http://38.181.35.75:8080/api/contract
🔗 正在请求: http://38.181.35.75:8080/api/contract?includeDisabled=false
📡 HTTP响应状态: OK
✅ 成功加载 203 个合约信息到缓存
```

**不应该是**：
```
🌐 API地址: http://localhost:8080/api/contract  ← 错误！
```

### 步骤3：验证量比功能

1. 打开程序
2. 点击"📊 综合信息仪表板"
3. 查看"量比最大TOP20"区域
4. 应该显示数据，而不是"暂无数据"

## 📊 配置对比

### 修改前

**问题**：
- 需要配置两个地址：`LicenseServerUrl` 和 `ContractApiServerUrl`
- 容易遗漏 `ContractApiServerUrl` 的配置
- 旧版本可能硬编码使用 `localhost:8080`

**配置**：
```xml
<add key="LicenseServerUrl" value="http://38.181.35.75:8080" />
<add key="ContractApiServerUrl" value="http://38.181.35.75:8080" />  <!-- 容易遗漏 -->
```

### 修改后

**优势**：
- 只需配置 `LicenseServerUrl`
- 自动回退使用 `LicenseServerUrl`
- 详细的日志显示配置源

**配置**：
```xml
<add key="LicenseServerUrl" value="http://38.181.35.75:8080" />
<!-- ContractApiServerUrl 可选，未配置时自动使用 LicenseServerUrl -->
```

## 💡 常见问题

### Q1：我需要删除 ContractApiServerUrl 配置吗？

**A**：不需要删除。如果保留，会优先使用它。如果删除或未配置，会自动使用 `LicenseServerUrl`。

### Q2：旧版本程序会受影响吗？

**A**：不会。这个修改完全向后兼容。旧版本仍然按原来的逻辑工作。

### Q3：如何确认使用的是哪个配置？

**A**：查看启动日志：
- `🔍 使用 ContractApiServerUrl: ...` → 使用的是 ContractApiServerUrl
- `🔍 ContractApiServerUrl 未配置，使用 LicenseServerUrl: ...` → 使用的是 LicenseServerUrl

### Q4：为什么还是显示 localhost:8080？

**A**：可能的原因：
1. 运行的是旧版本程序（未重新部署）
2. 配置文件中两个地址都未配置或配置错误
3. 配置文件未被正确读取

**解决方法**：
1. 使用 `发布并部署到SearchCoins.cmd` 重新部署
2. 检查配置文件内容
3. 查看完整的启动日志

## 📝 总结

### 修改内容

✅ 代码层面：合约API服务统一可回退使用 `LicenseServerUrl`  
✅ 配置层面：简化为只需配置一个服务器地址  
✅ 日志层面：详细显示使用的配置源  
✅ 部署层面：提供自动化部署脚本  

### 用户体验

✅ **更简单**：只需配置一个地址  
✅ **更清晰**：日志明确显示使用哪个配置  
✅ **更可靠**：自动回退机制确保功能可用  
✅ **更安全**：部署时自动保护配置和数据  

### 下一步

1. 运行 `发布并部署到SearchCoins.cmd`
2. 验证启动日志
3. 测试量比功能

---

**版本**：v2.3.3  
**修改日期**：2025年10月3日  
**影响范围**：合约API配置读取逻辑

