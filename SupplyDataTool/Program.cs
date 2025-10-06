using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using System.Linq; // Added for .Concat()

namespace SupplyDataTool
{
    class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔧 币安合约发行量数据维护工具");
            Console.WriteLine("=====================================");
            
            try
            {
                // 检查命令行参数
                if (args.Length > 0 && int.TryParse(args[0], out int option))
                {
                    await ExecuteOption(option);
                    return;
                }
                
                // 显示菜单
                ShowMenu();
                
                while (true)
                {
                    Console.Write("\n请选择操作 (1-6): ");
                    var choice = Console.ReadLine();
                    
                    if (int.TryParse(choice, out int choiceNum))
                    {
                        await ExecuteOption(choiceNum);
                        
                        if (choiceNum == 6)
                        {
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ 无效选择，请重试");
                    }
                    
                    Console.WriteLine("\n按任意键继续...");
                    Console.ReadKey();
                    ShowMenu();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 程序执行失败: {ex.Message}");
                if (args.Length == 0) // 只有交互模式才等待按键
                {
                    Console.WriteLine("\n按任意键退出...");
                    Console.ReadKey();
                }
            }
        }
        
        static async Task ExecuteOption(int option)
        {
            switch (option)
            {
                case 1:
                    await CreateExtendedSupplyDataAsync();
                    break;
                case 2:
                    await CreateAllFuturesContractsTemplateAsync();
                    break;
                case 3:
                    await AutoFillSupplyDataFromCoinGeckoAsync();
                    break;
                case 4:
                    await UpdateSingleContractAsync();
                    break;
                case 5:
                    await DisplayCurrentDataAsync();
                    break;
                case 6:
                    Console.WriteLine("👋 退出工具");
                    break;
                default:
                    Console.WriteLine("❌ 无效选择，请输入1-6");
                    break;
            }
        }
        
        static void ShowMenu()
        {
            Console.Clear();
            Console.WriteLine("🔧 币安合约发行量数据维护工具");
            Console.WriteLine("=====================================");
            Console.WriteLine("1. 创建扩展发行量数据文件 (包含50+主流合约)");
            Console.WriteLine("2. 从Binance获取所有永续合约并创建模板文件");
            Console.WriteLine("3. 自动从CoinGecko获取发行量数据并填写");
            Console.WriteLine("4. 更新单个合约发行量数据");
            Console.WriteLine("5. 显示当前数据文件内容");
            Console.WriteLine("6. 退出");
            Console.WriteLine("=====================================");
        }
        
        static async Task CreateAllFuturesContractsTemplateAsync()
        {
            Console.WriteLine("🔍 正在从Binance获取所有永续合约列表...");
            
            try
            {
                // 获取Binance永续合约交易信息
                var exchangeInfoUrl = "https://fapi.binance.com/fapi/v1/exchangeInfo";
                var response = await httpClient.GetStringAsync(exchangeInfoUrl);
                var exchangeInfo = JsonSerializer.Deserialize<JsonElement>(response);
                
                var symbols = new List<string>();
                if (exchangeInfo.TryGetProperty("symbols", out var symbolsArray))
                {
                    foreach (var symbol in symbolsArray.EnumerateArray())
                    {
                        if (symbol.TryGetProperty("symbol", out var symbolName) &&
                            symbol.TryGetProperty("status", out var status) &&
                            symbol.TryGetProperty("contractType", out var contractType) &&
                            status.GetString() == "TRADING" &&
                            contractType.GetString() == "PERPETUAL")
                        {
                            var symbolStr = symbolName.GetString();
                            if (!string.IsNullOrEmpty(symbolStr) && symbolStr.EndsWith("USDT"))
                            {
                                symbols.Add(symbolStr);
                            }
                        }
                    }
                }
                
                Console.WriteLine($"✅ 获取到 {symbols.Count} 个活跃的USDT永续合约");
                
                // 创建模板数据文件
                var supplyDataFile = new SupplyDataFile
                {
                    LastUpdated = DateTime.UtcNow,
                    Version = "1.0",
                    DataSources = new Dictionary<string, string>
                    {
                        ["Binance"] = "https://fapi.binance.com/fapi/v1/exchangeInfo",
                        ["CoinGecko"] = "https://api.coingecko.com/api/v3/",
                        ["Manual"] = "手动维护数据"
                    },
                    Contracts = new List<ContractSupplyData>()
                };
                
                // 为每个合约创建模板记录
                foreach (var symbol in symbols.OrderBy(s => s))
                {
                    var baseAsset = symbol.Replace("USDT", "");
                    supplyDataFile.Contracts.Add(new ContractSupplyData
                    {
                        Symbol = symbol,
                        BaseAsset = baseAsset,
                        CirculatingSupply = 0, // 需要手动填写
                        TotalSupply = 0,       // 需要手动填写
                        MaxSupply = 0,         // 需要手动填写
                        LastUpdated = DateTime.UtcNow,
                        DataSource = "Template"
                    });
                }
                
                // 创建输出目录
                var outputDir = "Output";
                Directory.CreateDirectory(outputDir);
                var outputPath = Path.Combine(outputDir, "all_futures_contracts_template.json");
                
                // 保存模板文件
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var json = JsonSerializer.Serialize(supplyDataFile, options);
                await File.WriteAllTextAsync(outputPath, json);
                
                Console.WriteLine($"✅ 永续合约模板文件已创建: {Path.GetFullPath(outputPath)}");
                Console.WriteLine($"📊 包含 {supplyDataFile.Contracts.Count} 个永续合约的模板记录");
                Console.WriteLine("\n📝 注意事项:");
                Console.WriteLine("  - 所有发行量数据初始值为0，需要手动填写");
                Console.WriteLine("  - 建议优先填写主流币种的发行量数据");
                Console.WriteLine("  - 可使用选项3逐个更新合约数据");
                Console.WriteLine("  - 填写完成后重命名为supply_data.json使用");
                
                // 显示一些统计信息
                var mainstreams = symbols.Where(s => IsMainstreamSymbol(s)).ToList();
                var defiTokens = symbols.Where(s => IsDeFiSymbol(s)).ToList();
                
                Console.WriteLine($"\n📈 合约分类统计:");
                Console.WriteLine($"  - 主流币种合约: {mainstreams.Count} 个");
                Console.WriteLine($"  - DeFi代币合约: {defiTokens.Count} 个");
                Console.WriteLine($"  - 其他代币合约: {symbols.Count - mainstreams.Count - defiTokens.Count} 个");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 获取永续合约列表失败: {ex.Message}");
                Console.WriteLine("请检查网络连接并重试");
            }
        }
        
        static bool IsMainstreamSymbol(string symbol)
        {
            var mainstream = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "XRPUSDT", "ADAUSDT", "SOLUSDT", "DOGEUSDT", "DOTUSDT", "AVAXUSDT", "SHIBUSDT", "LINKUSDT", "LTCUSDT", "MATICUSDT", "UNIUSDT", "ATOMUSDT" };
            return mainstream.Contains(symbol);
        }
        
        static bool IsDeFiSymbol(string symbol)
        {
            var defi = new[] { "AAVEUSDT", "COMPUSDT", "MKRUSDT", "SNXUSDT", "YFIUSDT", "CRVUSDT", "BALUSDT", "SUSHIUSDT", "1INCHUSDT", "CAKEUSDT" };
            return defi.Contains(symbol);
        }
        
        static async Task AutoFillSupplyDataFromCoinGeckoAsync()
        {
            Console.WriteLine("🔍 自动从CoinGecko获取发行量数据...");
            
            var templatePath = Path.Combine("Output", "all_futures_contracts_template.json");
            if (!File.Exists(templatePath))
            {
                Console.WriteLine("❌ 模板文件不存在，请先选择选项2创建模板文件");
                return;
            }
            
            try
            {
                // 读取模板文件
                var jsonContent = await File.ReadAllTextAsync(templatePath);
                var supplyDataFile = JsonSerializer.Deserialize<SupplyDataFile>(jsonContent);
                
                if (supplyDataFile?.Contracts == null)
                {
                    Console.WriteLine("❌ 模板文件格式错误");
                    return;
                }
                
                Console.WriteLine($"📊 开始处理 {supplyDataFile.Contracts.Count} 个合约...");
                
                // 首先获取CoinGecko的币种列表
                Console.WriteLine("🔄 获取CoinGecko币种列表...");
                var coinListUrl = "https://api.coingecko.com/api/v3/coins/list";
                var coinListResponse = await httpClient.GetStringAsync(coinListUrl);
                var coinList = JsonSerializer.Deserialize<JsonElement[]>(coinListResponse);
                
                // 创建symbol到id的映射
                var symbolToIdMap = new Dictionary<string, string>();
                if (coinList != null)
                {
                    foreach (var coin in coinList)
                    {
                        if (coin.TryGetProperty("symbol", out var symbol) && 
                            coin.TryGetProperty("id", out var id))
                        {
                            var symbolStr = symbol.GetString()?.ToUpper();
                            var idStr = id.GetString();
                            if (!string.IsNullOrEmpty(symbolStr) && !string.IsNullOrEmpty(idStr))
                            {
                                symbolToIdMap[symbolStr] = idStr;
                            }
                        }
                    }
                }
                
                Console.WriteLine($"✅ 获取到 {symbolToIdMap.Count} 个CoinGecko币种映射");
                
                int successCount = 0;
                int failedCount = 0;
                
                // 处理每个合约
                for (int i = 0; i < supplyDataFile.Contracts.Count; i++)
                {
                    var contract = supplyDataFile.Contracts[i];
                    var baseAsset = contract.BaseAsset;
                    
                    // 跳过已有数据的合约
                    if (contract.CirculatingSupply > 0)
                    {
                        Console.WriteLine($"⏭️  跳过已有数据的合约: {contract.Symbol}");
                        continue;
                    }
                    
                    // 查找CoinGecko ID
                    if (!symbolToIdMap.TryGetValue(baseAsset, out var coinGeckoId))
                    {
                        // 尝试一些常见的变体
                        var variants = new[] { baseAsset.ToLower(), $"wrapped-{baseAsset.ToLower()}", $"{baseAsset.ToLower()}-token" };
                        var found = false;
                        
                        foreach (var variant in variants)
                        {
                            if (symbolToIdMap.ContainsValue(variant))
                            {
                                coinGeckoId = variant;
                                found = true;
                                break;
                            }
                        }
                        
                        if (!found)
                        {
                            Console.WriteLine($"⚠️  无法找到 {baseAsset} 的CoinGecko ID");
                            failedCount++;
                            continue;
                        }
                    }
                    
                    try
                    {
                        // 获取币种详细信息
                        var coinUrl = $"https://api.coingecko.com/api/v3/coins/{coinGeckoId}";
                        var coinResponse = await httpClient.GetStringAsync(coinUrl);
                        var coinData = JsonSerializer.Deserialize<JsonElement>(coinResponse);
                        
                        if (coinData.TryGetProperty("market_data", out var marketData))
                        {
                            decimal circulatingSupply = 0;
                            decimal totalSupply = 0;
                            decimal maxSupply = 0;
                            
                            if (marketData.TryGetProperty("circulating_supply", out var circSupply) && 
                                circSupply.ValueKind == JsonValueKind.Number)
                            {
                                circulatingSupply = circSupply.GetDecimal();
                            }
                            
                            if (marketData.TryGetProperty("total_supply", out var totSupply) && 
                                totSupply.ValueKind == JsonValueKind.Number)
                            {
                                totalSupply = totSupply.GetDecimal();
                            }
                            
                            if (marketData.TryGetProperty("max_supply", out var maxSup) && 
                                maxSup.ValueKind == JsonValueKind.Number)
                            {
                                maxSupply = maxSup.GetDecimal();
                            }
                            
                            // 更新合约数据
                            contract.CirculatingSupply = circulatingSupply;
                            contract.TotalSupply = totalSupply > 0 ? totalSupply : circulatingSupply;
                            contract.MaxSupply = maxSupply;
                            contract.LastUpdated = DateTime.UtcNow;
                            contract.DataSource = "CoinGecko";
                            
                            Console.WriteLine($"✅ 更新 {contract.Symbol}: 流通量={circulatingSupply:N0}");
                            successCount++;
                        }
                        else
                        {
                            Console.WriteLine($"⚠️  {baseAsset} 无市场数据");
                            failedCount++;
                        }
                        
                        // 避免API限制，每次请求后等待
                        await Task.Delay(2000); // 增加到2秒延迟
                        
                        // 每10个合约显示进度
                        if ((i + 1) % 10 == 0)
                        {
                            Console.WriteLine($"📊 进度: {i + 1}/{supplyDataFile.Contracts.Count} ({successCount} 成功, {failedCount} 失败)");
                        }
                    }
                    catch (HttpRequestException ex) when (ex.Message.Contains("429"))
                    {
                        Console.WriteLine($"⏸️  API限制，等待30秒后重试 {baseAsset}...");
                        await Task.Delay(30000); // 遇到429错误时等待30秒
                        i--; // 重试当前合约
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ 获取 {baseAsset} 数据失败: {ex.Message}");
                        failedCount++;
                        await Task.Delay(5000); // 出错时等待5秒
                    }
                }
                
                // 保存更新后的数据
                supplyDataFile.LastUpdated = DateTime.UtcNow;
                var outputPath = Path.Combine("Output", "supply_data_filled.json");
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                var updatedJson = JsonSerializer.Serialize(supplyDataFile, options);
                await File.WriteAllTextAsync(outputPath, updatedJson);
                
                Console.WriteLine($"\n✅ 自动填写完成！");
                Console.WriteLine($"📊 统计结果:");
                Console.WriteLine($"  - 成功更新: {successCount} 个合约");
                Console.WriteLine($"  - 失败/跳过: {failedCount} 个合约");
                Console.WriteLine($"  - 数据文件: {Path.GetFullPath(outputPath)}");
                Console.WriteLine($"\n💡 提示: 可以重复运行此功能来补充失败的合约数据");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 自动填写发行量数据失败: {ex.Message}");
                Console.WriteLine("请检查网络连接和CoinGecko API可用性");
            }
        }
        
        static async Task CreateExtendedSupplyDataAsync()
        {
            Console.WriteLine("📊 正在创建扩展发行量数据文件...");
            
            var supplyDataFile = new SupplyDataFile
            {
                LastUpdated = DateTime.UtcNow,
                Version = "1.0",
                DataSources = new Dictionary<string, string>
                {
                    ["CoinGecko"] = "https://api.coingecko.com/api/v3/",
                    ["CoinMarketCap"] = "https://pro-api.coinmarketcap.com/v1/",
                    ["Manual"] = "手动维护数据"
                },
                Contracts = CreateExtendedContractsList()
            };
            
            // 创建输出目录
            var outputDir = "Output";
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, "supply_data.json");
            
            // 保存文件
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            var json = JsonSerializer.Serialize(supplyDataFile, options);
            await File.WriteAllTextAsync(outputPath, json);
            
            Console.WriteLine($"✅ 扩展发行量数据文件已创建: {Path.GetFullPath(outputPath)}");
            Console.WriteLine($"📊 包含 {supplyDataFile.Contracts.Count} 个合约的发行量数据");
            
            // 显示统计信息
            Console.WriteLine("\n📈 数据统计:");
            Console.WriteLine($"  - 主流币种: {supplyDataFile.Contracts.Count(c => IsMainstreamCoin(c.BaseAsset))} 个");
            Console.WriteLine($"  - DeFi代币: {supplyDataFile.Contracts.Count(c => IsDeFiToken(c.BaseAsset))} 个");
            Console.WriteLine($"  - 其他代币: {supplyDataFile.Contracts.Count(c => !IsMainstreamCoin(c.BaseAsset) && !IsDeFiToken(c.BaseAsset))} 个");
        }
        
        static List<ContractSupplyData> CreateExtendedContractsList()
        {
            var contracts = new List<ContractSupplyData>();
            
            // 主流币种 (Top 20 by Market Cap)
            var mainstreams = new[]
            {
                ("BTCUSDT", "BTC", 19750000m, 19750000m, 21000000m),
                ("ETHUSDT", "ETH", 120280000m, 120280000m, 0m),
                ("BNBUSDT", "BNB", 153856150m, 153856150m, 200000000m),
                ("XRPUSDT", "XRP", 54280538906m, 99986996740m, 100000000000m),
                ("ADAUSDT", "ADA", 35045020830m, 45000000000m, 45000000000m),
                ("SOLUSDT", "SOL", 467817394m, 580803434m, 0m),
                ("DOGEUSDT", "DOGE", 142140956384m, 142140956384m, 0m),
                ("DOTUSDT", "DOT", 1426000000m, 1426000000m, 0m),
                ("AVAXUSDT", "AVAX", 394220000m, 432220000m, 720000000m),
                ("SHIBUSDT", "SHIB", 589735030408323m, 999982336405194m, 1000000000000000m),
                ("LINKUSDT", "LINK", 538099971m, 1000000000m, 1000000000m),
                ("LTCUSDT", "LTC", 74730892m, 74730892m, 84000000m),
                ("MATICUSDT", "MATIC", 9319469069m, 10000000000m, 10000000000m),
                ("UNIUSDT", "UNI", 753766667m, 1000000000m, 1000000000m),
                ("ATOMUSDT", "ATOM", 389836387m, 389836387m, 0m),
                ("ETCUSDT", "ETC", 147315395m, 210700000m, 210700000m),
                ("XLMUSDT", "XLM", 27801595113m, 50001806812m, 50001806812m),
                ("VETUSDT", "VET", 72714516834m, 86712634466m, 86712634466m),
                ("ICPUSDT", "ICP", 498893398m, 523175000m, 523175000m),
                ("FILUSDT", "FIL", 579971817m, 2000000000m, 2000000000m)
            };
            
            // DeFi 代币
            var defiTokens = new[]
            {
                ("AAVEUSDT", "AAVE", 14093193m, 16000000m, 16000000m),
                ("COMPUSDT", "COMP", 10000000m, 10000000m, 10000000m),
                ("MKRUSDT", "MKR", 977631m, 1005577m, 1005577m),
                ("SNXUSDT", "SNX", 273469957m, 328226939m, 328226939m),
                ("YFIUSDT", "YFI", 36666m, 36666m, 36666m),
                ("CRVUSDT", "CRV", 1135787000m, 3303030299m, 3303030299m),
                ("BALRUSDT", "BAL", 35725926m, 100000000m, 100000000m),
                ("SUSHIUSDT", "SUSHI", 127244443m, 250000000m, 250000000m),
                ("1INCHUSDT", "1INCH", 1030000000m, 1500000000m, 1500000000m),
                ("CAKEUSDT", "CAKE", 315669239m, 750000000m, 750000000m)
            };
            
            // Layer 1/Layer 2 项目
            var layer1Tokens = new[]
            {
                ("NEARUSDT", "NEAR", 1092817781m, 1000000000m, 1000000000m),
                ("ALGOUSDT", "ALGO", 7279838486m, 10000000000m, 10000000000m),
                ("EGLDUSDT", "EGLD", 27130956m, 31415926m, 31415926m),
                ("FTMUSDT", "FTM", 2803634836m, 3175000000m, 3175000000m),
                ("ONEUSDT", "ONE", 12600000000m, 13170000000m, 13170000000m),
                ("ZILUSDT", "ZIL", 17320000000m, 21000000000m, 21000000000m),
                ("WAVESUSDT", "WAVES", 100000000m, 100000000m, 100000000m),
                ("HBARUSDT", "HBAR", 30396873817m, 50000000000m, 50000000000m),
                ("FLOWUSDT", "FLOW", 1386120304m, 1386120304m, 1386120304m),
                ("KSMUSDT", "KSM", 9993367m, 10000000m, 10000000m)
            };
            
            // NFT/Gaming 代币
            var nftTokens = new[]
            {
                ("AXSUSDT", "AXS", 148091675m, 270000000m, 270000000m),
                ("MANAUSDT", "MANA", 1893095371m, 2805886393m, 2805886393m),
                ("SANDUSDT", "SAND", 1821209814m, 3000000000m, 3000000000m),
                ("ENJUSDT", "ENJ", 1000000000m, 1000000000m, 1000000000m),
                ("CHZUSDT", "CHZ", 7822688756m, 8888888888m, 8888888888m),
                ("GALAUSDT", "GALA", 37145833333m, 50000000000m, 50000000000m),
                ("APECOINUSDT", "APE", 627500000m, 1000000000m, 1000000000m),
                ("GMTUSDT", "GMT", 2072762500m, 6000000000m, 6000000000m)
            };
            
            var now = DateTime.UtcNow;
            
            // 添加所有合约
            foreach (var (symbol, asset, circulating, total, max) in mainstreams.Concat(defiTokens).Concat(layer1Tokens).Concat(nftTokens))
            {
                contracts.Add(new ContractSupplyData
                {
                    Symbol = symbol,
                    BaseAsset = asset,
                    CirculatingSupply = circulating,
                    TotalSupply = total,
                    MaxSupply = max,
                    LastUpdated = now,
                    DataSource = "Manual"
                });
            }
            
            return contracts;
        }
        
        static bool IsMainstreamCoin(string asset)
        {
            var mainstream = new[] { "BTC", "ETH", "BNB", "XRP", "ADA", "SOL", "DOGE", "DOT", "AVAX", "SHIB", "LINK", "LTC", "MATIC", "UNI", "ATOM", "ETC", "XLM", "VET", "ICP", "FIL" };
            return mainstream.Contains(asset);
        }
        
        static bool IsDeFiToken(string asset)
        {
            var defi = new[] { "AAVE", "COMP", "MKR", "SNX", "YFI", "CRV", "BAL", "SUSHI", "1INCH", "CAKE" };
            return defi.Contains(asset);
        }
        
        static async Task UpdateSingleContractAsync()
        {
            Console.WriteLine("✏️ 更新单个合约发行量数据");
            Console.WriteLine("==============================");
            
            Console.Write("请输入合约代码 (如 BTCUSDT): ");
            var symbol = Console.ReadLine()?.ToUpper();
            if (string.IsNullOrEmpty(symbol))
            {
                Console.WriteLine("❌ 合约代码不能为空");
                return;
            }
            
            Console.Write("请输入流通供应量: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal circulating))
            {
                Console.WriteLine("❌ 流通供应量格式错误");
                return;
            }
            
            Console.Write("请输入总供应量: ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal total))
            {
                Console.WriteLine("❌ 总供应量格式错误");
                return;
            }
            
            Console.Write("请输入最大供应量 (0表示无上限): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal max))
            {
                Console.WriteLine("❌ 最大供应量格式错误");
                return;
            }
            
            // 更新现有文件或创建新文件
            var outputPath = Path.Combine("Output", "supply_data.json");
            SupplyDataFile supplyDataFile;
            
            if (File.Exists(outputPath))
            {
                var json = await File.ReadAllTextAsync(outputPath);
                supplyDataFile = JsonSerializer.Deserialize<SupplyDataFile>(json) ?? new SupplyDataFile();
            }
            else
            {
                supplyDataFile = new SupplyDataFile();
                Directory.CreateDirectory("Output");
            }
            
            var baseAsset = symbol.EndsWith("USDT") ? symbol.Replace("USDT", "") : symbol.Split('U')[0];
            var existingIndex = supplyDataFile.Contracts.FindIndex(c => c.Symbol == symbol);
            
            var newContract = new ContractSupplyData
            {
                Symbol = symbol,
                BaseAsset = baseAsset,
                CirculatingSupply = circulating,
                TotalSupply = total,
                MaxSupply = max,
                LastUpdated = DateTime.UtcNow,
                DataSource = "Manual"
            };
            
            if (existingIndex >= 0)
            {
                supplyDataFile.Contracts[existingIndex] = newContract;
                Console.WriteLine($"✅ 已更新 {symbol} 的发行量数据");
            }
            else
            {
                supplyDataFile.Contracts.Add(newContract);
                Console.WriteLine($"✅ 已添加 {symbol} 的发行量数据");
            }
            
            supplyDataFile.LastUpdated = DateTime.UtcNow;
            
            // 保存文件
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            var updatedJson = JsonSerializer.Serialize(supplyDataFile, options);
            await File.WriteAllTextAsync(outputPath, updatedJson);
            
            Console.WriteLine($"💾 数据已保存到: {Path.GetFullPath(outputPath)}");
        }
        
        static async Task DisplayCurrentDataAsync()
        {
            var outputPath = Path.Combine("Output", "supply_data.json");
            
            if (!File.Exists(outputPath))
            {
                Console.WriteLine("❌ 数据文件不存在，请先创建数据文件");
                return;
            }
            
            try
            {
                var json = await File.ReadAllTextAsync(outputPath);
                var supplyDataFile = JsonSerializer.Deserialize<SupplyDataFile>(json);
                
                if (supplyDataFile?.Contracts == null)
                {
                    Console.WriteLine("❌ 数据文件格式错误");
                    return;
                }
                
                Console.WriteLine("📊 当前发行量数据文件内容");
                Console.WriteLine("========================================");
                Console.WriteLine($"文件更新时间: {supplyDataFile.LastUpdated:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"合约总数: {supplyDataFile.Contracts.Count}");
                Console.WriteLine("========================================");
                
                // 按类别显示
                var mainstream = supplyDataFile.Contracts.Where(c => IsMainstreamCoin(c.BaseAsset)).OrderBy(c => c.Symbol).ToList();
                var defi = supplyDataFile.Contracts.Where(c => IsDeFiToken(c.BaseAsset)).OrderBy(c => c.Symbol).ToList();
                var others = supplyDataFile.Contracts.Where(c => !IsMainstreamCoin(c.BaseAsset) && !IsDeFiToken(c.BaseAsset)).OrderBy(c => c.Symbol).ToList();
                
                if (mainstream.Count > 0)
                {
                    Console.WriteLine($"\n🏆 主流币种 ({mainstream.Count} 个):");
                    foreach (var contract in mainstream)
                    {
                        Console.WriteLine($"  {contract.Symbol,-12} | 流通: {contract.CirculatingSupply:N0}");
                    }
                }
                
                if (defi.Count > 0)
                {
                    Console.WriteLine($"\n🏦 DeFi代币 ({defi.Count} 个):");
                    foreach (var contract in defi)
                    {
                        Console.WriteLine($"  {contract.Symbol,-12} | 流通: {contract.CirculatingSupply:N0}");
                    }
                }
                
                if (others.Count > 0)
                {
                    Console.WriteLine($"\n🔗 其他代币 ({others.Count} 个):");
                    foreach (var contract in others)
                    {
                        Console.WriteLine($"  {contract.Symbol,-12} | 流通: {contract.CirculatingSupply:N0}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 读取数据文件失败: {ex.Message}");
            }
        }
    }
} 