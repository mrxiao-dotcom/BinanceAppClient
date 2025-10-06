using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BinanceApps.Core.Extensions;
using BinanceApps.Core.Interfaces;
using BinanceApps.Trading.Interfaces;
using BinanceApps.Account.Interfaces;
using BinanceApps.MarketData.Interfaces;
using BinanceApps.Storage.Interfaces;
using BinanceApps.Core.Models;

namespace BinanceApps.Examples
{
    /// <summary>
    /// 使用示例
    /// </summary>
    public class UsageExamples
    {
        /// <summary>
        /// 示例1：仅使用交易模块
        /// </summary>
        public static async Task TradingModuleExample()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // 只注册交易模块
                    services.AddBinanceAppsTrading();
                })
                .Build();

            var tradingService = host.Services.GetRequiredService<ITradingService>();
            
            // 创建订单
            var order = new PerpetualOrder
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                Quantity = 0.001m,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC,
                Leverage = 10,
                MarginType = "isolated"
            };

            try
            {
                var result = await tradingService.PlaceOrderAsync(order);
                Console.WriteLine($"订单创建成功，订单ID: {result.OrderId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"订单创建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例2：仅使用账户模块
        /// </summary>
        public static async Task AccountModuleExample()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // 只注册账户模块
                    services.AddBinanceAppsAccount();
                })
                .Build();

            var accountService = host.Services.GetRequiredService<IAccountService>();
            
            try
            {
                // 获取账户信息
                var accountInfo = await accountService.GetAccountInfoAsync();
                Console.WriteLine($"总资产: {accountInfo.TotalWalletBalance} USDT");
                
                // 获取持仓信息
                var positions = await accountService.GetPositionsAsync();
                foreach (var position in positions)
                {
                    Console.WriteLine($"持仓: {position.Symbol}, 数量: {position.PositionAmt}, 盈亏: {position.UnrealizedPnl}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取账户信息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例3：仅使用行情数据模块
        /// </summary>
        public static async Task MarketDataModuleExample()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // 只注册行情数据模块
                    services.AddBinanceAppsMarketData();
                })
                .Build();

            var marketDataService = host.Services.GetRequiredService<IMarketDataService>();
            
            try
            {
                // 获取K线数据
                var klines = await marketDataService.GetKlinesAsync("BTCUSDT", KlineInterval.OneHour, 100);
                Console.WriteLine($"获取到 {klines.Count} 条K线数据");
                
                // 获取24小时价格统计
                var priceStats = await marketDataService.GetPriceStatisticsAsync("BTCUSDT");
                if (priceStats != null)
                {
                    Console.WriteLine($"BTCUSDT 最新价格: {priceStats.LastPrice}, 24小时涨跌幅: {priceStats.PriceChangePercent}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取行情数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例4：仅使用存储模块
        /// </summary>
        public static async Task StorageModuleExample()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // 只注册存储模块
                    services.AddBinanceAppsStorage();
                })
                .Build();

            var storageService = host.Services.GetRequiredService<IFileStorageService>();
            var logService = host.Services.GetRequiredService<ILogStorageService>();
            var configService = host.Services.GetRequiredService<IConfigurationStorageService>();
            
            try
            {
                // 写入配置
                await configService.SetConfigurationAsync("LastLoginTime", DateTime.Now);
                await configService.SetConfigurationAsync("UserPreferences", new { Theme = "Dark", Language = "zh-CN" });
                
                // 读取配置
                var lastLoginTime = await configService.GetConfigurationAsync<DateTime>("LastLoginTime");
                Console.WriteLine($"上次登录时间: {lastLoginTime}");
                
                // 写入日志
                await logService.WriteInfoAsync("应用程序启动成功", "System");
                await logService.WriteWarningAsync("API调用频率较高", "Trading");
                
                // 写入JSON文件
                var data = new List<PriceStatistics>
                {
                    new PriceStatistics { Symbol = "BTCUSDT", LastPrice = 50000m, Volume = 1000m }
                };
                await storageService.WriteJsonAsync("price_data.json", data);
                
                Console.WriteLine("存储操作完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"存储操作失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例5：使用所有模块的完整应用
        /// </summary>
        public static async Task FullApplicationExample()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // 注册所有模块
                    services.AddBinanceApps();
                })
                .Build();

            var tradingService = host.Services.GetRequiredService<ITradingService>();
            var accountService = host.Services.GetRequiredService<IAccountService>();
            var marketDataService = host.Services.GetRequiredService<IMarketDataService>();
            var storageService = host.Services.GetRequiredService<IFileStorageService>();
            var logService = host.Services.GetRequiredService<ILogStorageService>();
            
            try
            {
                // 记录启动日志
                await logService.WriteInfoAsync("完整应用启动", "System");
                
                // 获取账户信息
                var accountInfo = await accountService.GetAccountInfoAsync();
                await logService.WriteInfoAsync($"账户总资产: {accountInfo.TotalWalletBalance} USDT", "Account");
                
                // 获取行情数据
                var priceStats = await marketDataService.GetPriceStatisticsAsync("BTCUSDT");
                if (priceStats != null)
                {
                    await logService.WriteInfoAsync($"BTCUSDT 当前价格: {priceStats.LastPrice}", "MarketData");
                    
                    // 如果价格低于某个阈值，可以考虑买入
                    if (priceStats.LastPrice < 45000m)
                    {
                        var order = new PerpetualOrder
                        {
                            Symbol = "BTCUSDT",
                            Side = OrderSide.Buy,
                            OrderType = OrderType.Limit,
                            Quantity = 0.001m,
                            Price = priceStats.LastPrice * 0.99m, // 比当前价格低1%
                            TimeInForce = TimeInForce.GTC,
                            Leverage = 10
                        };
                        
                        var result = await tradingService.PlaceOrderAsync(order);
                        await logService.WriteInfoAsync($"自动下单成功，订单ID: {result.OrderId}", "Trading");
                    }
                }
                
                // 保存数据到本地
                var marketData = new
                {
                    Timestamp = DateTime.Now,
                    AccountInfo = accountInfo,
                    PriceStats = priceStats
                };
                await storageService.WriteJsonAsync("market_snapshot.json", marketData);
                
                await logService.WriteInfoAsync("完整应用运行完成", "System");
                Console.WriteLine("完整应用示例运行成功");
            }
            catch (Exception ex)
            {
                await logService.WriteErrorAsync($"应用运行失败: {ex.Message}", "System", ex);
                Console.WriteLine($"应用运行失败: {ex.Message}");
            }
        }
    }
} 