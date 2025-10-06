using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 发行量数据服务
    /// </summary>
    public class SupplyDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SupplyDataService>? _logger;
        private readonly string _dataFilePath;
        private readonly Dictionary<string, ContractSupplyData> _supplyCache;
        private DateTime _lastCacheUpdate;

        public SupplyDataService(HttpClient httpClient, ILogger<SupplyDataService>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "supply_data.json");
            _supplyCache = new Dictionary<string, ContractSupplyData>();
            _lastCacheUpdate = DateTime.MinValue;
            
            // 确保数据目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(_dataFilePath)!);
        }

        /// <summary>
        /// 初始化服务，加载本地数据到缓存
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await LoadSupplyDataFromFileAsync();
                _logger?.LogInformation($"✅ 发行量数据服务初始化完成，缓存了 {_supplyCache.Count} 个合约的数据");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ 发行量数据服务初始化失败");
                Console.WriteLine($"⚠️ 发行量数据服务初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载发行量数据到缓存
        /// </summary>
        private async Task LoadSupplyDataFromFileAsync()
        {
            if (!File.Exists(_dataFilePath))
            {
                Console.WriteLine("📂 发行量数据文件不存在，将创建默认文件");
                await CreateDefaultSupplyDataFileAsync();
                return;
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(_dataFilePath);
                var supplyDataFile = JsonSerializer.Deserialize<SupplyDataFile>(jsonContent);
                
                if (supplyDataFile?.Contracts != null)
                {
                    _supplyCache.Clear();
                    foreach (var contract in supplyDataFile.Contracts.Where(c => c.IsValid))
                    {
                        _supplyCache[contract.Symbol] = contract;
                    }
                    _lastCacheUpdate = supplyDataFile.LastUpdated;
                    Console.WriteLine($"📊 已加载 {_supplyCache.Count} 个合约的发行量数据到缓存");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "加载发行量数据文件失败");
                Console.WriteLine($"⚠️ 加载发行量数据文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建默认的发行量数据文件
        /// </summary>
        private async Task CreateDefaultSupplyDataFileAsync()
        {
            var defaultData = new SupplyDataFile
            {
                LastUpdated = DateTime.UtcNow,
                Version = "1.0",
                DataSources = new Dictionary<string, string>
                {
                    ["CoinGecko"] = "https://api.coingecko.com/api/v3/",
                    ["Manual"] = "手动维护数据"
                },
                Contracts = new List<ContractSupplyData>
                {
                    // 添加一些主流币种的默认数据
                    new ContractSupplyData
                    {
                        Symbol = "BTCUSDT",
                        BaseAsset = "BTC",
                        CirculatingSupply = 19750000m,
                        TotalSupply = 19750000m,
                        MaxSupply = 21000000m,
                        LastUpdated = DateTime.UtcNow,
                        DataSource = "Manual"
                    },
                    new ContractSupplyData
                    {
                        Symbol = "ETHUSDT",
                        BaseAsset = "ETH",
                        CirculatingSupply = 120280000m,
                        TotalSupply = 120280000m,
                        MaxSupply = 0m, // ETH没有固定上限
                        LastUpdated = DateTime.UtcNow,
                        DataSource = "Manual"
                    },
                    new ContractSupplyData
                    {
                        Symbol = "BNBUSDT",
                        BaseAsset = "BNB",
                        CirculatingSupply = 153856150m,
                        TotalSupply = 153856150m,
                        MaxSupply = 200000000m,
                        LastUpdated = DateTime.UtcNow,
                        DataSource = "Manual"
                    }
                }
            };

            var json = JsonSerializer.Serialize(defaultData, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            
            await File.WriteAllTextAsync(_dataFilePath, json);
            Console.WriteLine($"📁 已创建默认发行量数据文件: {_dataFilePath}");
            
            // 加载到缓存
            foreach (var contract in defaultData.Contracts)
            {
                _supplyCache[contract.Symbol] = contract;
            }
            _lastCacheUpdate = defaultData.LastUpdated;
        }

        /// <summary>
        /// 获取合约的发行量数据
        /// </summary>
        public ContractSupplyData? GetSupplyData(string symbol)
        {
            return _supplyCache.TryGetValue(symbol, out var data) ? data : null;
        }

        /// <summary>
        /// 获取所有缓存的发行量数据
        /// </summary>
        public Dictionary<string, ContractSupplyData> GetAllSupplyData()
        {
            return new Dictionary<string, ContractSupplyData>(_supplyCache);
        }

        /// <summary>
        /// 计算市值数据
        /// </summary>
        public MarketCapData? CalculateMarketCap(string symbol, decimal currentPrice)
        {
            var supplyData = GetSupplyData(symbol);
            if (supplyData == null || currentPrice <= 0)
                return null;

            var marketCap = currentPrice * supplyData.CirculatingSupply;
            var fullyDilutedCap = supplyData.MaxSupply > 0 ? currentPrice * supplyData.MaxSupply : marketCap;

            return new MarketCapData
            {
                Symbol = symbol,
                BaseAsset = supplyData.BaseAsset,
                CurrentPrice = currentPrice,
                CirculatingSupply = supplyData.CirculatingSupply,
                MarketCap = marketCap,
                FullyDilutedCap = fullyDilutedCap,
                CalculatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 批量计算市值数据并排名
        /// </summary>
        public List<MarketCapData> CalculateMarketCapsWithRanking(Dictionary<string, decimal> symbolPrices)
        {
            var marketCaps = new List<MarketCapData>();

            foreach (var (symbol, price) in symbolPrices)
            {
                var marketCapData = CalculateMarketCap(symbol, price);
                if (marketCapData != null)
                {
                    marketCaps.Add(marketCapData);
                }
            }

            // 按市值排序并设置排名
            var rankedMarketCaps = marketCaps
                .OrderByDescending(m => m.MarketCap)
                .Select((m, index) => 
                {
                    m.MarketCapRank = index + 1;
                    return m;
                })
                .ToList();

            Console.WriteLine($"📈 计算了 {rankedMarketCaps.Count} 个合约的市值数据");
            return rankedMarketCaps;
        }

        /// <summary>
        /// 更新单个合约的发行量数据
        /// </summary>
        public async Task<bool> UpdateSupplyDataAsync(string symbol, decimal circulatingSupply, decimal totalSupply, decimal maxSupply, string dataSource = "Manual")
        {
            try
            {
                var baseAsset = symbol.EndsWith("USDT") ? symbol.Replace("USDT", "") : symbol;
                
                var supplyData = new ContractSupplyData
                {
                    Symbol = symbol,
                    BaseAsset = baseAsset,
                    CirculatingSupply = circulatingSupply,
                    TotalSupply = totalSupply,
                    MaxSupply = maxSupply,
                    LastUpdated = DateTime.UtcNow,
                    DataSource = dataSource
                };

                _supplyCache[symbol] = supplyData;
                await SaveSupplyDataToFileAsync();
                
                Console.WriteLine($"✅ 更新 {symbol} 发行量数据: 流通量={circulatingSupply:N0}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"更新 {symbol} 发行量数据失败");
                Console.WriteLine($"⚠️ 更新 {symbol} 发行量数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存缓存数据到文件
        /// </summary>
        private async Task SaveSupplyDataToFileAsync()
        {
            try
            {
                var supplyDataFile = new SupplyDataFile
                {
                    LastUpdated = DateTime.UtcNow,
                    Version = "1.0",
                    DataSources = new Dictionary<string, string>
                    {
                        ["CoinGecko"] = "https://api.coingecko.com/api/v3/",
                        ["Manual"] = "手动维护数据"
                    },
                    Contracts = _supplyCache.Values.ToList()
                };

                var json = JsonSerializer.Serialize(supplyDataFile, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                await File.WriteAllTextAsync(_dataFilePath, json);
                _lastCacheUpdate = supplyDataFile.LastUpdated;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存发行量数据到文件失败");
                Console.WriteLine($"⚠️ 保存发行量数据到文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int Count, DateTime LastUpdate) GetCacheStats()
        {
            return (_supplyCache.Count, _lastCacheUpdate);
        }

        /// <summary>
        /// 清理过期数据（超过30天未更新的数据）
        /// </summary>
        public async Task CleanupExpiredDataAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var expiredSymbols = _supplyCache
                    .Where(kvp => kvp.Value.LastUpdated < cutoffDate)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var symbol in expiredSymbols)
                {
                    _supplyCache.Remove(symbol);
                }

                if (expiredSymbols.Count > 0)
                {
                    await SaveSupplyDataToFileAsync();
                    Console.WriteLine($"🧹 清理了 {expiredSymbols.Count} 个过期的发行量数据");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "清理过期数据失败");
                Console.WriteLine($"⚠️ 清理过期数据失败: {ex.Message}");
            }
        }
    }
} 