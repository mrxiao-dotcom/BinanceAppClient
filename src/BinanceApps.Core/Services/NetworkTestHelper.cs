using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 网络测试帮助类
    /// </summary>
    public static class NetworkTestHelper
    {
        /// <summary>
        /// 测试网络连接
        /// </summary>
        /// <returns>网络连接测试结果</returns>
        public static async Task<NetworkTestResult> TestNetworkConnectionAsync()
        {
            var result = new NetworkTestResult();
            
            // 创建支持代理的HttpClient
            var handler = new HttpClientHandler()
            {
                UseProxy = true,
                UseDefaultCredentials = true
            };
            
            using var httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // 测试基本网络连接
            Console.WriteLine("🔍 测试网络连接...");
            
            // 1. 测试币安API连接
            try
            {
                Console.WriteLine("🔍 测试币安API连接 (api.binance.com)...");
                var binanceResponse = await httpClient.GetAsync("https://api.binance.com/api/v3/ping");
                result.BinanceApiReachable = binanceResponse.IsSuccessStatusCode;
                Console.WriteLine($"🔍 币安API: {(result.BinanceApiReachable ? "✅ 可达" : "❌ 不可达")}");
            }
            catch (Exception ex)
            {
                result.BinanceApiReachable = false;
                result.BinanceApiError = ex.Message;
                Console.WriteLine($"🔍 币安API: ❌ 连接失败 - {ex.Message}");
            }
            
            // 2. 测试币安测试网连接
            try
            {
                Console.WriteLine("🔍 测试币安测试网连接 (testnet.binance.vision)...");
                var testnetResponse = await httpClient.GetAsync("https://testnet.binance.vision/api/v3/ping");
                result.BinanceTestnetReachable = testnetResponse.IsSuccessStatusCode;
                Console.WriteLine($"🔍 币安测试网: {(result.BinanceTestnetReachable ? "✅ 可达" : "❌ 不可达")}");
            }
            catch (Exception ex)
            {
                result.BinanceTestnetReachable = false;
                result.BinanceTestnetError = ex.Message;
                Console.WriteLine($"🔍 币安测试网: ❌ 连接失败 - {ex.Message}");
            }
            
            // 3. 测试一般网络连接
            try
            {
                Console.WriteLine("🔍 测试一般网络连接 (httpbin.org)...");
                var generalResponse = await httpClient.GetAsync("https://httpbin.org/ip");
                result.GeneralNetworkReachable = generalResponse.IsSuccessStatusCode;
                Console.WriteLine($"🔍 一般网络: {(result.GeneralNetworkReachable ? "✅ 可达" : "❌ 不可达")}");
                
                if (result.GeneralNetworkReachable)
                {
                    var ipInfo = await generalResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"🔍 当前IP信息: {ipInfo}");
                }
            }
            catch (Exception ex)
            {
                result.GeneralNetworkReachable = false;
                result.GeneralNetworkError = ex.Message;
                Console.WriteLine($"🔍 一般网络: ❌ 连接失败 - {ex.Message}");
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// 网络测试结果
    /// </summary>
    public class NetworkTestResult
    {
        public bool BinanceApiReachable { get; set; }
        public string? BinanceApiError { get; set; }
        
        public bool BinanceTestnetReachable { get; set; }
        public string? BinanceTestnetError { get; set; }
        
        public bool GeneralNetworkReachable { get; set; }
        public string? GeneralNetworkError { get; set; }
        
        public bool HasAnyConnection => BinanceApiReachable || BinanceTestnetReachable || GeneralNetworkReachable;
    }
} 