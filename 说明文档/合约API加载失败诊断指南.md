# 合约API加载失败诊断指南

## 🔴 错误信息

```
🔍 检查合约信息缓存状态: IsCacheLoaded=False, CachedCount=0
⚠️ 合约信息缓存未加载，无法计算量比排行
```

## 📋 问题分析

**症状**：量比排行显示"暂无数据（需要加载合约流通量信息）"

**根本原因**：应用启动时，从合约API服务器加载流通量信息失败

## 🔍 诊断步骤

### 第1步：查看启动日志

重新启动程序，查看控制台输出，应该在启动阶段看到类似：

```
📊 正在从本地API加载合约流通量信息...
🌐 API地址: http://38.181.35.75:8080/api/contract
🔗 正在请求: http://38.181.35.75:8080/api/contract?includeDisabled=false
📡 HTTP响应状态: [这里会显示状态]
```

**关键信息**：`HTTP响应状态` 会告诉我们具体问题。

### 第2步：检查API服务器状态

#### 方法1：浏览器测试（推荐）

在浏览器地址栏输入：
```
http://38.181.35.75:8080/api/contract?includeDisabled=false
```

**预期结果**：
```json
{
  "success": true,
  "data": [
    {
      "name": "BTCUSDT",
      "symbol": "BTC",
      "totalSupply": 21000000,
      "circulatingSupply": 19000000,
      "decimals": 8
    },
    ...
  ]
}
```

**如果看到错误**：
- `无法访问此网站` → API服务器未运行或网络问题
- `404 Not Found` → API接口路径不存在
- `500 Internal Server Error` → API服务器内部错误
- 其他JSON格式 → 检查是否符合预期格式

#### 方法2：PowerShell测试

```powershell
Invoke-WebRequest -Uri "http://38.181.35.75:8080/api/contract?includeDisabled=false" -Method GET
```

#### 方法3：curl测试（如果安装了）

```bash
curl http://38.181.35.75:8080/api/contract?includeDisabled=false
```

### 第3步：检查网络连接

```powershell
# 测试服务器是否可访问
Test-NetConnection -ComputerName 38.181.35.75 -Port 8080
```

**预期输出**：
```
TcpTestSucceeded : True  ← 表示端口开放
```

如果显示 `False`，说明：
- API服务器未启动
- 端口被防火墙阻止
- IP地址或端口配置错误

## 🛠️ 解决方案

### 方案1：API服务器未启动

**问题**：合约API服务器没有运行

**解决方法**：
1. 启动合约API服务器
2. 确认服务器监听在 `8080` 端口
3. 重新启动本程序

### 方案2：API地址或端口错误

**问题**：配置的地址或端口不正确

**解决方法**：
1. 确认正确的API服务器地址和端口
2. 修改 `App.config` 的 `ContractApiServerUrl` 配置：
   ```xml
   <add key="ContractApiServerUrl" value="http://正确的地址:端口" />
   ```
3. 重新启动程序

### 方案3：API接口路径不存在（404）

**问题**：API路径 `/api/contract` 不存在

**可能原因**：
- API路径改变了
- API版本不匹配
- API服务器配置问题

**解决方法**：
1. 检查API服务器文档，确认正确的接口路径
2. 如果路径不同，需要修改代码：
   ```csharp
   // src/BinanceApps.Core/Services/ContractInfoService.cs 第46行
   var url = $"{_baseUrl}/api/contract?includeDisabled=false";
   // 改为正确的路径
   ```

### 方案4：API返回格式不正确

**问题**：API返回的JSON格式与预期不符

**解决方法**：
1. 在浏览器访问API，复制返回的JSON
2. 对比预期格式：
   ```json
   {
     "success": true,
     "data": [
       {
         "name": "BTCUSDT",     // 关键字段
         "symbol": "BTC",        // 可选
         "circulatingSupply": 19000000  // 关键字段
       }
     ]
   }
   ```
3. 如果格式不同，需要调整数据模型或解析逻辑

### 方案5：网络连接问题

**问题**：无法连接到API服务器

**解决方法**：
1. 检查防火墙设置
2. 检查网络连接
3. 如果是远程服务器，确认VPN是否连接
4. 尝试 `ping 38.181.35.75` 测试网络连通性

### 方案6：临时禁用量比功能

**如果暂时无法解决API问题**：

量比功能会优雅降级：
- ✅ 仪表板其他功能正常使用
- ✅ 量比排行区域显示"暂无数据（需要加载合约流通量信息）"
- ✅ 不影响24H涨跌幅、高低位置等其他功能

**后续可以在API服务器恢复后，重启程序即可正常使用量比功能。**

## 📊 常见错误及对应解决方案

| 错误信息 | 原因 | 解决方案 |
|---------|------|---------|
| `无法连接到合约信息API (http://...)` | 网络连接失败 | 检查网络、防火墙、服务器状态 |
| `HTTP响应状态: NotFound` | API路径不存在 | 确认API路径是否正确 |
| `HTTP响应状态: InternalServerError` | API服务器错误 | 检查API服务器日志 |
| `API返回数据为空或失败` | API返回格式不正确 | 检查API返回的JSON格式 |
| `接收到数据，长度: 0 字节` | API无响应内容 | 检查API是否正确返回数据 |

## 🔧 调试技巧

### 技巧1：开启详细日志

启动程序后，查看控制台的完整输出：
```
📊 正在从本地API加载合约流通量信息...
🌐 API地址: http://38.181.35.75:8080/api/contract
🔗 正在请求: http://38.181.35.75:8080/api/contract?includeDisabled=false
📡 HTTP响应状态: OK
📦 接收到数据，长度: 15234 字节
🔍 解析结果 - Success: True, Data Count: 204
✅ API返回成功，共 204 个合约
  📝 缓存: XPLUSDT -> XPLUSDT, 流通量: 1,000,000
  ...
✅ 成功加载 203 个合约信息到缓存
```

### 技巧2：使用Postman或API测试工具

1. 安装Postman或类似工具
2. 创建GET请求：`http://38.181.35.75:8080/api/contract?includeDisabled=false`
3. 查看响应状态码和内容
4. 验证数据格式

### 技巧3：检查API服务器日志

如果您有API服务器的访问权限：
1. 查看API服务器的访问日志
2. 查看是否有来自客户端的请求
3. 查看API处理请求时是否有错误

## 📝 配置文件说明

**配置位置**：`src/BinanceApps.WPF/App.config`

```xml
<!-- Contract API Server Configuration -->
<add key="ContractApiServerUrl" value="http://38.181.35.75:8080" />
```

**如何修改**：
1. 用文本编辑器打开 `App.config`
2. 找到 `ContractApiServerUrl` 配置行
3. 修改 `value` 为正确的API地址
4. 保存文件
5. 重新启动程序

**注意事项**：
- 地址格式：`http://IP:端口` 或 `http://域名:端口`
- 不要在末尾添加 `/`
- 确保端口号正确
- 如果使用HTTPS，请写 `https://`

## 🎯 快速检查清单

在报告问题前，请先完成以下检查：

- [ ] 在浏览器访问API接口，确认是否能正常访问
- [ ] 检查API服务器是否运行（`Test-NetConnection`）
- [ ] 查看程序启动时的完整控制台输出
- [ ] 确认 `App.config` 中的 `ContractApiServerUrl` 配置正确
- [ ] 尝试 `ping` API服务器IP，确认网络连通性
- [ ] 检查防火墙是否阻止了8080端口

## 💡 联系支持时需要提供的信息

如果以上方法都无法解决问题，请提供以下信息：

1. **启动时的完整控制台输出**（特别是包含"📊 正在从本地API加载..."的部分）
2. **浏览器访问API的结果**（截图或复制返回的JSON）
3. **`Test-NetConnection`的输出**
4. **`App.config`中的`ContractApiServerUrl`配置**
5. **API服务器的类型和版本**（如果知道）

这些信息将帮助快速定位问题！


