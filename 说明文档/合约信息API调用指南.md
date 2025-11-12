# 合约信息 API 调用指南

> **用途**: 获取合约的总发行量、流通量等基本信息  
> **版本**: v1.0  
> **最后更新**: 2025-10-21

---

## 📋 目录

1. [API 基础信息](#api-基础信息)
2. [认证说明](#认证说明)
3. [API 端点](#api-端点)
4. [使用示例](#使用示例)
5. [错误处理](#错误处理)
6. [常见问题](#常见问题)

---

## API 基础信息

### 服务器地址

```csharp
// 定义服务器地址变量
string serverUrl = "http://YOUR_SERVER_IP:8080";

// 或使用 HTTPS
string serverUrl = "https://YOUR_SERVER_IP:8080";
```

### 基础 URL

```
{serverUrl}/api/contract
```

### 请求格式

- **Content-Type**: `application/json`
- **Accept**: `application/json`
- **字符编码**: `UTF-8`

---

## 认证说明

当前版本为**公开 API**，无需认证即可查询。

> ⚠️ 注意：管理功能（创建、更新、删除）需要在服务器端进行，不对外开放。

---

## API 端点

### 1. 根据合约名称获取信息 ⭐ (推荐)

**端点**: `GET {serverUrl}/api/contract/name/{name}`

**说明**: 通过合约名称或代币符号获取合约详细信息

**URL 参数**:
- `name` (string, 必需): 合约名称或代币符号
  - **精确匹配**: 必须完全匹配数据库中的名称
  - **大小写敏感**: `BTC` 和 `btc` 是不同的
  - **不支持模糊查询**: 如需模糊查询请使用搜索接口

**⚠️ 重要说明：参数格式**

| 输入参数 | 结果 | 说明 |
|---------|------|------|
| `BTC` | ✅ 成功 | 如果数据库中存储的是 `BTC` |
| `BTCUSDT` | ✅ 成功 | 如果数据库中存储的是 `BTCUSDT` |
| `btc` | ❌ 失败 | 大小写不匹配 |
| `BT` | ❌ 失败 | 不支持部分匹配 |

**关键点**:
1. **数据库中存储什么，就查询什么** - 如果合约在数据库中的名称是 `BTCUSDT`，那么必须用 `BTCUSDT` 查询
2. **BTC vs BTCUSDT** - 这是两个完全不同的记录：
   - `BTC`: 可能代表比特币本身
   - `BTCUSDT`: 可能代表 BTC/USDT 交易对
3. **建议先使用搜索接口** - 如果不确定准确名称，先用搜索接口查询

**响应字段**:

| 字段 | 类型 | 说明 |
|------|------|------|
| `success` | boolean | 请求是否成功 |
| `data.name` | string | 合约名称/代币符号 |
| `data.totalSupply` | decimal | **总发行量** |
| `data.circulatingSupply` | decimal | **流通量** |
| `data.contractAddress` | string | 合约地址（可选） |
| `data.symbol` | string | 代币符号（补充字段） |
| `data.description` | string | 简介 |
| `data.decimals` | int | 小数位数 |

**示例 1: 查询 BTC**

```http
GET http://YOUR_SERVER_IP:8080/api/contract/name/BTC
```

**示例 2: 查询 BTCUSDT**

```http
GET http://YOUR_SERVER_IP:8080/api/contract/name/BTCUSDT
```

**⚠️ 注意**: 这两个是不同的查询，会返回不同的数据（如果数据库中都存在）

**成功响应**:

```json
{
  "success": true,
  "data": {
    "name": "BTC",
    "totalSupply": 21000000,
    "circulatingSupply": 19000000,
    "contractAddress": "0xbtc1234567890abcdef",
    "symbol": "Bitcoin",
    "description": "比特币 - 第一个去中心化的加密货币",
    "decimals": 8
  }
}
```

**失败响应**:

```json
{
  "success": false,
  "message": "合约不存在或未启用"
}
```

---

### 2. 搜索合约 ⭐ (支持模糊查询)

**端点**: `GET {serverUrl}/api/contract/search`

**说明**: 根据关键词搜索合约，**支持模糊匹配**

**查询参数**:
- `keyword` (string, 可选): 搜索关键词
  - **支持模糊查询**: 会匹配包含关键词的所有记录
  - **大小写不敏感**: 自动转换为小写匹配
  - **多字段搜索**: 同时搜索名称、符号、地址、描述
- `includeDisabled` (bool, 可选): 是否包含禁用的合约，默认 `false`

**✨ 模糊查询特性**:

| 关键词 | 匹配结果示例 | 说明 |
|--------|-------------|------|
| `BTC` | `BTC`, `BTCUSDT`, `WBTC` | 包含 BTC 的所有记录 |
| `USD` | `USDT`, `USDC`, `BTCUSDT`, `ETHUSDT` | 包含 USD 的所有记录 |
| `bitcoin` | `BTC` (如果描述中包含 bitcoin) | 搜索描述字段 |
| 空字符串 | 所有合约 | 返回全部启用的合约 |

**使用建议**:
1. **不确定准确名称时使用搜索** - 输入部分关键词即可
2. **查看所有相关合约** - 例如搜索 `USDT` 可以看到所有 USDT 交易对
3. **先搜索后精确查询** - 搜索确定名称后，再用精确查询接口获取详情

**响应**: 返回合约列表数组

**示例**:

```http
GET http://YOUR_SERVER_IP:8080/api/contract/search?keyword=BTC
```

**响应**:

```json
{
  "success": true,
  "data": [
    {
      "name": "BTC",
      "totalSupply": 21000000,
      "circulatingSupply": 19000000,
      "contractAddress": "0xbtc1234567890abcdef",
      "symbol": "Bitcoin",
      "description": "比特币 - 第一个去中心化的加密货币",
      "decimals": 8
    }
  ]
}
```

---

### 3. 获取所有合约

**端点**: `GET {serverUrl}/api/contract`

**说明**: 获取所有已启用的合约列表

**查询参数**:
- `includeDisabled` (bool, 可选): 是否包含禁用的合约，默认 `false`

**响应**: 返回合约列表数组

**示例**:

```http
GET http://YOUR_SERVER_IP:8080/api/contract
```

---

### 4. 根据 ID 获取信息

**端点**: `GET {serverUrl}/api/contract/{id}`

**说明**: 通过数据库 ID 获取合约信息（主要用于管理功能）

**URL 参数**:
- `id` (int, 必需): 合约的数据库 ID

**示例**:

```http
GET http://YOUR_SERVER_IP:8080/api/contract/1
```

---

## 使用示例

### C# / .NET 示例

#### 1. 使用 HttpClient（推荐）

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class ContractApiClient
{
    private readonly string _serverUrl;
    private readonly HttpClient _httpClient;
    
    public ContractApiClient(string serverUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }
    
    /// <summary>
    /// 根据合约名称获取信息
    /// </summary>
    public async Task<ContractInfo> GetContractByNameAsync(string name)
    {
        try
        {
            string url = $"{_serverUrl}/api/contract/name/{Uri.EscapeDataString(name)}";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse<ContractData>>(json);
                
                if (result?.Success == true && result.Data != null)
                {
                    return new ContractInfo
                    {
                        Name = result.Data.Name,
                        TotalSupply = result.Data.TotalSupply,
                        CirculatingSupply = result.Data.CirculatingSupply,
                        ContractAddress = result.Data.ContractAddress,
                        Symbol = result.Data.Symbol,
                        Description = result.Data.Description,
                        Decimals = result.Data.Decimals
                    };
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取合约信息失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 搜索合约
    /// </summary>
    public async Task<List<ContractInfo>> SearchContractsAsync(string keyword)
    {
        try
        {
            string url = $"{_serverUrl}/api/contract/search?keyword={Uri.EscapeDataString(keyword)}";
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ApiResponse<List<ContractData>>>(json);
                
                if (result?.Success == true && result.Data != null)
                {
                    return result.Data.Select(d => new ContractInfo
                    {
                        Name = d.Name,
                        TotalSupply = d.TotalSupply,
                        CirculatingSupply = d.CirculatingSupply,
                        ContractAddress = d.ContractAddress,
                        Symbol = d.Symbol,
                        Description = d.Description,
                        Decimals = d.Decimals
                    }).ToList();
                }
            }
            
            return new List<ContractInfo>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"搜索合约失败: {ex.Message}");
            return new List<ContractInfo>();
        }
    }
}

// 数据模型
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
}

public class ContractData
{
    public string Name { get; set; }
    public decimal TotalSupply { get; set; }
    public decimal CirculatingSupply { get; set; }
    public string ContractAddress { get; set; }
    public string Symbol { get; set; }
    public string Description { get; set; }
    public int Decimals { get; set; }
}

public class ContractInfo
{
    public string Name { get; set; }
    public decimal TotalSupply { get; set; }
    public decimal CirculatingSupply { get; set; }
    public string ContractAddress { get; set; }
    public string Symbol { get; set; }
    public string Description { get; set; }
    public int Decimals { get; set; }
}
```

#### 使用示例

```csharp
// 初始化客户端
string serverUrl = "http://192.168.1.100:8080";  // 替换为您的服务器地址
var client = new ContractApiClient(serverUrl);

// 获取 BTC 信息
var btcInfo = await client.GetContractByNameAsync("BTC");
if (btcInfo != null)
{
    Console.WriteLine($"合约名称: {btcInfo.Name}");
    Console.WriteLine($"总发行量: {btcInfo.TotalSupply:N0}");
    Console.WriteLine($"流通量: {btcInfo.CirculatingSupply:N0}");
    Console.WriteLine($"流通比例: {(btcInfo.CirculatingSupply / btcInfo.TotalSupply * 100):F2}%");
}

// 搜索合约
var searchResults = await client.SearchContractsAsync("USD");
foreach (var contract in searchResults)
{
    Console.WriteLine($"{contract.Name}: 总量 {contract.TotalSupply:N0}, 流通 {contract.CirculatingSupply:N0}");
}
```

---

### JavaScript / TypeScript 示例

```javascript
// 服务器地址配置
const SERVER_URL = 'http://YOUR_SERVER_IP:8080';

/**
 * 根据合约名称获取信息
 */
async function getContractByName(name) {
    try {
        const response = await fetch(`${SERVER_URL}/api/contract/name/${encodeURIComponent(name)}`);
        const result = await response.json();
        
        if (result.success && result.data) {
            return {
                name: result.data.name,
                totalSupply: result.data.totalSupply,
                circulatingSupply: result.data.circulatingSupply,
                contractAddress: result.data.contractAddress,
                symbol: result.data.symbol,
                description: result.data.description,
                decimals: result.data.decimals
            };
        }
        
        return null;
    } catch (error) {
        console.error('获取合约信息失败:', error);
        return null;
    }
}

/**
 * 搜索合约
 */
async function searchContracts(keyword) {
    try {
        const response = await fetch(
            `${SERVER_URL}/api/contract/search?keyword=${encodeURIComponent(keyword)}`
        );
        const result = await response.json();
        
        if (result.success && result.data) {
            return result.data;
        }
        
        return [];
    } catch (error) {
        console.error('搜索合约失败:', error);
        return [];
    }
}

// 使用示例
async function example() {
    // 获取 BTC 信息
    const btc = await getContractByName('BTC');
    if (btc) {
        console.log(`合约名称: ${btc.name}`);
        console.log(`总发行量: ${btc.totalSupply.toLocaleString()}`);
        console.log(`流通量: ${btc.circulatingSupply.toLocaleString()}`);
        console.log(`流通比例: ${(btc.circulatingSupply / btc.totalSupply * 100).toFixed(2)}%`);
    }
    
    // 搜索合约
    const results = await searchContracts('USD');
    results.forEach(contract => {
        console.log(`${contract.name}: 总量 ${contract.totalSupply.toLocaleString()}, 流通 ${contract.circulatingSupply.toLocaleString()}`);
    });
}
```

---

### Python 示例

```python
import requests
from typing import Optional, List, Dict

# 服务器地址配置
SERVER_URL = "http://YOUR_SERVER_IP:8080"

class ContractApiClient:
    def __init__(self, server_url: str):
        self.server_url = server_url.rstrip('/')
        
    def get_contract_by_name(self, name: str) -> Optional[Dict]:
        """根据合约名称获取信息"""
        try:
            url = f"{self.server_url}/api/contract/name/{name}"
            response = requests.get(url, timeout=30)
            
            if response.status_code == 200:
                result = response.json()
                if result.get('success') and result.get('data'):
                    return result['data']
            
            return None
        except Exception as e:
            print(f"获取合约信息失败: {e}")
            return None
    
    def search_contracts(self, keyword: str) -> List[Dict]:
        """搜索合约"""
        try:
            url = f"{self.server_url}/api/contract/search"
            params = {'keyword': keyword}
            response = requests.get(url, params=params, timeout=30)
            
            if response.status_code == 200:
                result = response.json()
                if result.get('success') and result.get('data'):
                    return result['data']
            
            return []
        except Exception as e:
            print(f"搜索合约失败: {e}")
            return []

# 使用示例
if __name__ == "__main__":
    # 初始化客户端
    client = ContractApiClient("http://192.168.1.100:8080")
    
    # 获取 BTC 信息
    btc = client.get_contract_by_name("BTC")
    if btc:
        print(f"合约名称: {btc['name']}")
        print(f"总发行量: {btc['totalSupply']:,}")
        print(f"流通量: {btc['circulatingSupply']:,}")
        print(f"流通比例: {(btc['circulatingSupply'] / btc['totalSupply'] * 100):.2f}%")
    
    # 搜索合约
    results = client.search_contracts("USD")
    for contract in results:
        print(f"{contract['name']}: 总量 {contract['totalSupply']:,}, 流通 {contract['circulatingSupply']:,}")
```

---

### cURL 示例

```bash
# 定义服务器地址变量
SERVER_URL="http://YOUR_SERVER_IP:8080"

# 1. 获取 BTC 信息
curl -X GET "${SERVER_URL}/api/contract/name/BTC" \
  -H "Accept: application/json"

# 2. 搜索包含 "USD" 的合约
curl -X GET "${SERVER_URL}/api/contract/search?keyword=USD" \
  -H "Accept: application/json"

# 3. 获取所有合约
curl -X GET "${SERVER_URL}/api/contract" \
  -H "Accept: application/json"

# 4. 根据 ID 获取信息
curl -X GET "${SERVER_URL}/api/contract/1" \
  -H "Accept: application/json"
```

---

## 错误处理

### HTTP 状态码

| 状态码 | 说明 |
|--------|------|
| 200 | 请求成功 |
| 400 | 请求参数错误 |
| 404 | 资源不存在 |
| 500 | 服务器内部错误 |

### 错误响应格式

```json
{
  "success": false,
  "message": "错误描述信息"
}
```

### 常见错误

#### 1. 合约不存在

**请求**: `GET /api/contract/name/NOTEXIST`

**响应**:
```json
{
  "success": false,
  "message": "合约不存在或未启用"
}
```

#### 2. 网络连接失败

**C# 处理**:
```csharp
try
{
    var result = await client.GetContractByNameAsync("BTC");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"网络连接失败: {ex.Message}");
    Console.WriteLine("请检查服务器地址和网络连接");
}
catch (TaskCanceledException ex)
{
    Console.WriteLine("请求超时");
}
```

#### 3. JSON 解析失败

**JavaScript 处理**:
```javascript
try {
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    const result = await response.json();
    // 处理结果
} catch (error) {
    console.error('请求失败:', error.message);
}
```

---

## 常见问题

### Q1: BTC 和 BTCUSDT 有什么区别？应该查询哪个？🔥

**A**: 这是两个**完全不同**的记录：

```csharp
// 场景 1: 数据库中存储的是交易对名称（带 USDT 后缀）
var btcUsdt = await client.GetContractByNameAsync("BTCUSDT");  // ✅ 正确
var btc = await client.GetContractByNameAsync("BTC");          // ❌ 找不到

// 场景 2: 数据库中存储的是代币本身（不带后缀）
var btc = await client.GetContractByNameAsync("BTC");          // ✅ 正确
var btcUsdt = await client.GetContractByNameAsync("BTCUSDT");  // ❌ 找不到
```

**如何判断应该用哪个名称？**

1. **方法 1: 先用搜索接口** (推荐)
```csharp
// 搜索包含 BTC 的所有记录
var results = await client.SearchContractsAsync("BTC");
// 查看结果中的准确名称，然后再精确查询
foreach (var item in results)
{
    Console.WriteLine($"数据库中的名称: {item.Name}");
}
```

2. **方法 2: 查看服务器管理页面**
   - 打开 `http://YOUR_SERVER_IP:8080/contract-management.html`
   - 查看合约列表中的实际名称

3. **方法 3: 使用异常处理兜底**
```csharp
async Task<ContractInfo> GetContractSmart(string baseName)
{
    // 先尝试不带后缀
    var result = await client.GetContractByNameAsync(baseName);
    if (result != null) return result;
    
    // 再尝试带 USDT 后缀
    result = await client.GetContractByNameAsync($"{baseName}USDT");
    if (result != null) return result;
    
    // 最后使用搜索
    var searchResults = await client.SearchContractsAsync(baseName);
    return searchResults.FirstOrDefault();
}
```

**典型应用场景对应关系**:

| 数据来源 | 通常格式 | 示例 |
|---------|---------|------|
| 交易所 API | 带交易对后缀 | `BTCUSDT`, `ETHUSDT` |
| 区块链浏览器 | 代币符号 | `BTC`, `ETH`, `USDT` |
| 自定义系统 | 取决于设计 | 需要查看实际数据 |

---

### Q2: 是否支持模糊查询？区分大小写吗？🔥

**A**: 两个接口的行为不同：

**1. 精确查询接口 (`/api/contract/name/{name}`)**
- ❌ 不支持模糊查询
- ✅ 区分大小写
- 必须完全匹配

```csharp
// 假设数据库中存储的是 "BTCUSDT"
await client.GetContractByNameAsync("BTCUSDT");  // ✅ 找到
await client.GetContractByNameAsync("btcusdt");  // ❌ 找不到（大小写不匹配）
await client.GetContractByNameAsync("BTC");      // ❌ 找不到（不完全匹配）
```

**2. 搜索接口 (`/api/contract/search`)**
- ✅ 支持模糊查询
- ✅ 不区分大小写
- 匹配包含关键词的所有记录

```csharp
// 搜索所有包含 "btc" 的记录（不区分大小写）
await client.SearchContractsAsync("btc");   // ✅ 返回 BTC, BTCUSDT, WBTC 等
await client.SearchContractsAsync("BTC");   // ✅ 同上（自动转小写）
await client.SearchContractsAsync("usd");   // ✅ 返回 USDT, USDC, BTCUSDT 等
```

**选择建议**:
- 知道准确名称 → 使用精确查询接口（更快）
- 不确定名称 → 使用搜索接口（更灵活）

---

### Q3: 如何配置服务器地址？

**A**: 根据您的部署环境设置：

```csharp
// 开发环境
string serverUrl = "http://localhost:8080";

// 生产环境（局域网）
string serverUrl = "http://192.168.1.100:8080";

// 生产环境（公网）
string serverUrl = "https://api.yourcompany.com";
```

### Q4: 总发行量和流通量的区别？

**A**: 
- **总发行量 (totalSupply)**: 代币的总供应量，通常是固定的
- **流通量 (circulatingSupply)**: 当前市场上实际流通的代币数量
- **流通比例**: `circulatingSupply / totalSupply * 100%`

### Q5: 如何处理大数字？

**A**: 使用 `decimal` 类型和小数位数：

```csharp
// 原始值
decimal totalSupply = 21000000;
int decimals = 8;

// 实际值（考虑小数位）
decimal actualValue = totalSupply / (decimal)Math.Pow(10, decimals);
// 结果: 0.21 BTC

// 格式化显示
Console.WriteLine($"{totalSupply:N0}");  // 21,000,000
```

### Q6: 如何缓存查询结果？

**A**: 建议实现本地缓存：

```csharp
private Dictionary<string, (ContractInfo Info, DateTime CachedAt)> _cache = new();
private TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

public async Task<ContractInfo> GetContractWithCacheAsync(string name)
{
    // 检查缓存
    if (_cache.TryGetValue(name, out var cached))
    {
        if (DateTime.Now - cached.CachedAt < _cacheExpiration)
        {
            return cached.Info;
        }
    }
    
    // 从 API 获取
    var info = await GetContractByNameAsync(name);
    if (info != null)
    {
        _cache[name] = (info, DateTime.Now);
    }
    
    return info;
}
```

### Q7: 支持批量查询吗？

**A**: 当前版本不支持批量查询。如需获取多个合约，可以：

```csharp
// 方式1: 并行查询
var tasks = new[] { "BTC", "ETH", "USDT" }
    .Select(name => client.GetContractByNameAsync(name));
var results = await Task.WhenAll(tasks);

// 方式2: 使用搜索接口（如果有共同关键词）
var allContracts = await client.SearchContractsAsync("");
```

---

## 完整示例：控制台应用

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ContractInfoApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 配置服务器地址
            Console.Write("请输入服务器地址 (默认 http://localhost:8080): ");
            string serverUrl = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                serverUrl = "http://localhost:8080";
            }
            
            var client = new ContractApiClient(serverUrl);
            
            Console.WriteLine("\n合约信息查询系统");
            Console.WriteLine("================\n");
            
            while (true)
            {
                Console.Write("请输入合约名称 (输入 'exit' 退出): ");
                string input = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
                {
                    break;
                }
                
                Console.WriteLine("\n查询中...");
                var contract = await client.GetContractByNameAsync(input);
                
                if (contract != null)
                {
                    Console.WriteLine($"\n✅ 查询成功！");
                    Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine($"合约名称: {contract.Name}");
                    Console.WriteLine($"代币符号: {contract.Symbol}");
                    Console.WriteLine($"总发行量: {contract.TotalSupply:N0}");
                    Console.WriteLine($"流通数量: {contract.CirculatingSupply:N0}");
                    Console.WriteLine($"流通比例: {(contract.CirculatingSupply / contract.TotalSupply * 100):F2}%");
                    Console.WriteLine($"小数位数: {contract.Decimals}");
                    if (!string.IsNullOrEmpty(contract.Description))
                    {
                        Console.WriteLine($"简介: {contract.Description}");
                    }
                    Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
                }
                else
                {
                    Console.WriteLine($"\n❌ 未找到合约 '{input}'\n");
                }
            }
            
            Console.WriteLine("感谢使用！");
        }
    }
}
```

---

## 技术支持

### 文档资源
- 📚 合约管理系统使用指南
- 📋 服务器部署文档
- 🔧 API 参考文档

### 联系方式
- 📧 邮箱: support@yourcompany.com
- 💬 技术支持: 内部工单系统

---

**文档版本**: v1.0  
**最后更新**: 2025-10-21  
**维护者**: RegisterSrv Team

---

## 快速参考卡片

```
┌─────────────────────────────────────────────────────────────┐
│  合约信息 API 快速参考                                       │
├─────────────────────────────────────────────────────────────┤
│  基础地址: {serverUrl}/api/contract                          │
├─────────────────────────────────────────────────────────────┤
│  精确查询: GET /name/{name}                                  │
│    - 必须完全匹配，区分大小写                                │
│    - 例: BTC ≠ BTCUSDT (两个不同的记录)                      │
│                                                              │
│  模糊搜索: GET /search?keyword={keyword}  ⭐ 推荐           │
│    - 支持模糊查询，不区分大小写                              │
│    - 例: 搜索 "btc" 返回 BTC, BTCUSDT, WBTC...              │
│                                                              │
│  全部合约: GET /                                             │
│  按ID查询: GET /{id}                                         │
├─────────────────────────────────────────────────────────────┤
│  关键字段:                                                   │
│  - name: 合约名称 (BTC 或 BTCUSDT 等)                        │
│  - totalSupply: 总发行量                                     │
│  - circulatingSupply: 流通量                                 │
│  - decimals: 小数位数                                        │
├─────────────────────────────────────────────────────────────┤
│  💡 使用建议:                                               │
│  1. 不确定名称？先用搜索接口                                 │
│  2. 知道准确名称？用精确查询（更快）                         │
│  3. BTC vs BTCUSDT？查看 Q1 FAQ                             │
└─────────────────────────────────────────────────────────────┘
```

