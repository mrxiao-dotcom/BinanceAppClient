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

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== 币安API测试程序 ===");
        
        // 1. 时间戳测试
        TestTimestamp();
        
        // 2. 读取配置
        var config = ReadConfig();
        if (config == null) return;
        
        // 3. 网络测试
        await TestNetwork(config.IsTestnet);
        
        // 4. API测试
        await TestApi(config);
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
    
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
    
    static Config? ReadConfig()
    {
        try
        {
            if (!File.Exists("src/BinanceApps.WPF/appsettings.json"))
            {
                Console.WriteLine("❌ 找不到配置文件");
                return null;
            }
            
            var json = File.ReadAllText("src/BinanceApps.WPF/appsettings.json");
            var doc = JsonDocument.Parse(json);
            var api = doc.RootElement.GetProperty("BinanceApi");
            
            var apiKey = api.GetProperty("ApiKey").GetString();
            var secretKey = api.GetProperty("SecretKey").GetString();
            
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
            {
                Console.WriteLine("❌ API Key或Secret Key为空");
                return null;
            }
            
            Console.WriteLine($"✅ API Key: {apiKey[..Math.Min(12, apiKey.Length)]}...");
            
            return new Config
            {
                ApiKey = apiKey,
                SecretKey = secretKey,
                IsTestnet = api.GetProperty("IsTestnet").GetBoolean()
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 读取配置失败: {ex.Message}");
            return null;
        }
    }
    
    static async Task TestNetwork(bool isTestnet)
    {
        Console.WriteLine("\n--- 网络测试 ---");
        var url = isTestnet ? "https://testnet.binance.vision" : "https://api.binance.com";
        
        using var http = new HttpClient();
        try
        {
            var response = await http.GetAsync($"{url}/api/v3/ping");
            Console.WriteLine($"Ping: {(response.IsSuccessStatusCode ? "✅" : "❌")} {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 网络错误: {ex.Message}");
        }
    }
    
    static async Task TestApi(Config config)
    {
        Console.WriteLine("\n--- API测试 ---");
        var url = config.IsTestnet ? "https://testnet.binance.vision" : "https://api.binance.com";
        
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-MBX-APIKEY", config.ApiKey);
        
        try
        {
            // 获取服务器时间
            var timeResp = await http.GetAsync($"{url}/api/v3/time");
            var timeJson = await timeResp.Content.ReadAsStringAsync();
            var timeDoc = JsonDocument.Parse(timeJson);
            var serverTime = timeDoc.RootElement.GetProperty("serverTime").GetInt64();
            
            Console.WriteLine($"服务器时间: {DateTimeOffset.FromUnixTimeMilliseconds(serverTime):yyyy-MM-dd HH:mm:ss} UTC");
            
            // 构建测试订单
            var timestamp = serverTime.ToString(CultureInfo.InvariantCulture);
            var parameters = new Dictionary<string, string>
            {
                {"symbol", "BTCUSDT"},
                {"side", "BUY"},
                {"type", "LIMIT"},
                {"timeInForce", "GTC"},
                {"quantity", "0.001"},
                {"price", "20000"},
                {"timestamp", timestamp},
                {"recvWindow", "10000"}
            };
            
            var query = string.Join("&", parameters.OrderBy(p => p.Key).Select(p => $"{p.Key}={p.Value}"));
            var signature = GenerateSignature(query, config.SecretKey);
            
            Console.WriteLine($"时间戳: {timestamp}");
            Console.WriteLine($"签名: {signature[..20]}...");
            
            // 发送测试订单
            var testUrl = $"{url}/api/v3/order/test?{query}&signature={signature}";
            var testResp = await http.PostAsync(testUrl, null);
            var testContent = await testResp.Content.ReadAsStringAsync();
            
            if (testResp.IsSuccessStatusCode)
            {
                Console.WriteLine("✅ API测试成功");
            }
            else
            {
                Console.WriteLine($"❌ API测试失败: {testResp.StatusCode}");
                Console.WriteLine($"错误: {testContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ API异常: {ex.Message}");
        }
    }
    
    static string GenerateSignature(string query, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var queryBytes = Encoding.UTF8.GetBytes(query);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(queryBytes);
        return Convert.ToHexString(hash).ToLower();
    }
    
    class Config
    {
        public string ApiKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public bool IsTestnet { get; set; }
    }
} 