using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

Console.WriteLine("=== 币安API测试程序 ===");

// 1. 时间戳测试
TestTimestamp();

// 2. 创建默认配置（只用于获取行情数据）
var config = new Config("", "", false); // 不需要真实API Key

// 3. 网络测试
await TestNetwork(config.IsTestnet);

// 4. 行情数据API测试
await TestApi(config);

Console.WriteLine("\n按任意键退出...");
Console.ReadKey();

static void TestTimestamp()
{
    Console.WriteLine("\n--- 时间戳测试 ---");
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    
    // 测试不同文化
    var cultures = new[] { 
        CultureInfo.InvariantCulture, 
        CultureInfo.CurrentCulture 
    };
    
    foreach (var culture in cultures)
    {
        var str = timestamp.ToString(culture);
        var valid = Regex.IsMatch(str, @"^[0-9]{1,20}$");
        Console.WriteLine($"{culture.Name}: '{str}' - {(valid ? "✅" : "❌")}");
    }
    
    // 安全时间戳
    var safe = timestamp.ToString(CultureInfo.InvariantCulture);
    safe = Regex.Replace(safe, @"[^0-9]", "");
    Console.WriteLine($"安全时间戳: '{safe}' - {(Regex.IsMatch(safe, @"^[0-9]{1,20}$") ? "✅" : "❌")}");
}

// ReadConfig方法已移除，不再需要

static async Task TestNetwork(bool isTestnet)
{
    Console.WriteLine("\n--- 网络测试 ---");
    var url = isTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";
    
    using var http = new HttpClient();
    try
    {
        var response = await http.GetAsync($"{url}/fapi/v1/ping");
        Console.WriteLine($"Futures Ping: {(response.IsSuccessStatusCode ? "✅" : "❌")} {response.StatusCode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 网络错误: {ex.Message}");
    }
}

static async Task TestApi(Config config)
{
    Console.WriteLine("\n--- 行情数据API测试 ---");
    var url = config.IsTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com";
    
    using var http = new HttpClient();
    // 不添加API Key，测试公开行情数据
    
    try
    {
        // 1. 获取服务器时间
        var timeResp = await http.GetAsync($"{url}/fapi/v1/time");
        if (timeResp.IsSuccessStatusCode)
        {
            var timeJson = await timeResp.Content.ReadAsStringAsync();
            var timeDoc = JsonDocument.Parse(timeJson);
            var serverTime = timeDoc.RootElement.GetProperty("serverTime").GetInt64();
            Console.WriteLine($"✅ 服务器时间: {DateTimeOffset.FromUnixTimeMilliseconds(serverTime):yyyy-MM-dd HH:mm:ss} UTC");
        }
        else
        {
            Console.WriteLine($"❌ 获取服务器时间失败: {timeResp.StatusCode}");
        }
        
        // 2. 获取交易规则信息
        var exchangeInfoResp = await http.GetAsync($"{url}/fapi/v1/exchangeInfo");
        if (exchangeInfoResp.IsSuccessStatusCode)
        {
            var exchangeInfoJson = await exchangeInfoResp.Content.ReadAsStringAsync();
            var exchangeDoc = JsonDocument.Parse(exchangeInfoJson);
            var symbols = exchangeDoc.RootElement.GetProperty("symbols");
            Console.WriteLine($"✅ 交易对信息获取成功，共 {symbols.GetArrayLength()} 个交易对");
        }
        else
        {
            Console.WriteLine($"❌ 获取交易规则失败: {exchangeInfoResp.StatusCode}");
        }
        
        // 3. 获取BTCUSDT价格信息
        var priceResp = await http.GetAsync($"{url}/fapi/v1/ticker/price?symbol=BTCUSDT");
        if (priceResp.IsSuccessStatusCode)
        {
            var priceJson = await priceResp.Content.ReadAsStringAsync();
            var priceDoc = JsonDocument.Parse(priceJson);
            var symbol = priceDoc.RootElement.GetProperty("symbol").GetString();
            var price = priceDoc.RootElement.GetProperty("price").GetString();
            Console.WriteLine($"✅ {symbol} 当前价格: ${price}");
        }
        else
        {
            Console.WriteLine($"❌ 获取价格信息失败: {priceResp.StatusCode}");
        }
        
        // 4. 获取深度信息
        var depthResp = await http.GetAsync($"{url}/fapi/v1/depth?symbol=BTCUSDT&limit=5");
        if (depthResp.IsSuccessStatusCode)
        {
            var depthJson = await depthResp.Content.ReadAsStringAsync();
            var depthDoc = JsonDocument.Parse(depthJson);
            var bids = depthDoc.RootElement.GetProperty("bids");
            var asks = depthDoc.RootElement.GetProperty("asks");
            Console.WriteLine($"✅ 深度信息获取成功，买盘 {bids.GetArrayLength()} 档，卖盘 {asks.GetArrayLength()} 档");
            
            if (bids.GetArrayLength() > 0)
            {
                var bestBid = bids[0];
                Console.WriteLine($"   最佳买价: ${bestBid[0].GetString()} 数量: {bestBid[1].GetString()}");
            }
            if (asks.GetArrayLength() > 0)
            {
                var bestAsk = asks[0];
                Console.WriteLine($"   最佳卖价: ${bestAsk[0].GetString()} 数量: {bestAsk[1].GetString()}");
            }
        }
        else
        {
            Console.WriteLine($"❌ 获取深度信息失败: {depthResp.StatusCode}");
        }
        
        Console.WriteLine("\n🎉 所有公开行情数据API测试完成！");
        Console.WriteLine("✅ 无需API Key即可正常获取行情数据");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ API异常: {ex.Message}");
    }
}

// GenerateSignature方法已移除，不再需要

record Config(string ApiKey, string SecretKey, bool IsTestnet);
