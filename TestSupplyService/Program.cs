using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using BinanceApps.Core.Services;
using System.Linq; // Added for .Take()

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🧪 测试发行量数据服务...");
        
        try
        {
            var httpClient = new HttpClient();
            var supplyService = new SupplyDataService(httpClient);
            
            Console.WriteLine("📊 初始化发行量数据服务...");
            await supplyService.InitializeAsync();
            
            var (count, lastUpdate) = supplyService.GetCacheStats();
            Console.WriteLine($"✅ 缓存统计: {count} 个合约，最后更新: {lastUpdate:yyyy-MM-dd HH:mm}");
            
            // 测试市值计算
            Console.WriteLine("\n💰 测试市值计算:");
            var testPrices = new Dictionary<string, decimal>
            {
                ["BTCUSDT"] = 45000m,
                ["ETHUSDT"] = 2500m,
                ["BNBUSDT"] = 300m
            };
            
            foreach (var (symbol, price) in testPrices)
            {
                var marketCapData = supplyService.CalculateMarketCap(symbol, price);
                if (marketCapData != null)
                {
                    Console.WriteLine($"  {symbol}: 价格=${price:N0}, 市值={marketCapData.FormattedMarketCap}");
                }
                else
                {
                    Console.WriteLine($"  {symbol}: 无发行量数据");
                }
            }
            
            // 测试批量市值计算和排名
            Console.WriteLine("\n📈 测试批量市值计算和排名:");
            var marketCaps = supplyService.CalculateMarketCapsWithRanking(testPrices);
            foreach (var mc in marketCaps.Take(5))
            {
                Console.WriteLine($"  #{mc.MarketCapRank} {mc.Symbol}: {mc.FormattedMarketCap}");
            }
            
            Console.WriteLine("\n✅ 发行量数据服务测试完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 测试失败: {ex.Message}");
            Console.WriteLine($"详细错误: {ex}");
        }
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
} 