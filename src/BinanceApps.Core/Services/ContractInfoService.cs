using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BinanceApps.Core.Models;
using Microsoft.Extensions.Logging;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 合约信息服务 - 从自定义API获取合约流通量等信息
    /// </summary>
    public class ContractInfoService
    {
        private readonly ILogger<ContractInfoService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        
        // 缓存：合约符号(USDT) -> 合约信息
        private Dictionary<string, ContractInfo> _contractCache = new();
        private DateTime _lastLoadTime = DateTime.MinValue;
        private bool _hasShownCacheKeySample = false; // 标记是否已显示过缓存键示例
        private HashSet<string> _loggedMissingSymbols = new(); // 记录已输出日志的缺失合约
        
        public ContractInfoService(ILogger<ContractInfoService> logger, string baseUrl = "http://localhost:8080")
        {
            _logger = logger;
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
        
        /// <summary>
        /// 启动时加载所有合约信息到缓存
        /// </summary>
        public async Task<bool> LoadContractInfoAsync()
        {
            try
            {
                _logger.LogInformation("开始从API加载合约信息...");
                Console.WriteLine($"📊 开始从API加载合约流通量信息...");
                Console.WriteLine($"🌐 API地址: {_baseUrl}/api/contract");
                
                var url = $"{_baseUrl}/api/contract?includeDisabled=false";
                Console.WriteLine($"🔗 正在请求: {url}");
                
                var response = await _httpClient.GetAsync(url);
                Console.WriteLine($"📡 HTTP响应状态: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"API请求失败: {response.StatusCode}");
                    Console.WriteLine($"❌ API请求失败: {response.StatusCode}");
                    return false;
                }
                
                var jsonString = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"📦 接收到数据，长度: {jsonString.Length} 字节");
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var apiResponse = JsonSerializer.Deserialize<ContractListApiResponse>(jsonString, options);
                Console.WriteLine($"🔍 解析结果 - Success: {apiResponse?.Success}, Data Count: {apiResponse?.Data?.Count ?? 0}");
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    Console.WriteLine($"✅ API返回成功，共 {apiResponse.Data.Count} 个合约");
                    
                    // 构建缓存：BTCUSDT -> BTC合约信息
                    _contractCache.Clear();
                    foreach (var contract in apiResponse.Data)
                    {
                        if (!string.IsNullOrEmpty(contract.Name))
                        {
                            // API返回的Name字段已经包含USDT后缀，直接使用
                            var symbol = contract.Name.ToUpper();
                            
                            // 过滤掉无效的符号（如Excel错误值 #VALUE!）
                            if (symbol.Contains("#") || symbol.Contains("!") || symbol.Contains("ERROR"))
                            {
                                Console.WriteLine($"  ⏭️ 跳过无效合约: {contract.Name}");
                                continue;
                            }
                            
                            _contractCache[symbol] = contract;
                            Console.WriteLine($"  📝 缓存: {contract.Name} -> {symbol}, 流通量: {contract.CirculatingSupply:N0}");
                        }
                    }
                    
                _lastLoadTime = DateTime.Now;
                _logger.LogInformation($"成功加载 {_contractCache.Count} 个合约信息到缓存");
                Console.WriteLine($"✅ 成功加载 {_contractCache.Count} 个合约信息到缓存");
                
                // 检查主流币种是否在缓存中
                var mainCoins = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "SOLUSDT", "XRPUSDT" };
                Console.WriteLine("🔍 检查主流币种缓存情况：");
                foreach (var coin in mainCoins)
                {
                    if (_contractCache.ContainsKey(coin))
                    {
                        var info = _contractCache[coin];
                        Console.WriteLine($"   ✅ {coin}: 流通量={info.CirculatingSupply:N0}");
                    }
                    else
                    {
                        Console.WriteLine($"   ❌ {coin}: 不在缓存中");
                    }
                }
                
                return true;
                }
                else
                {
                    _logger.LogWarning("API返回数据为空或失败");
                    Console.WriteLine($"⚠️ API返回数据为空或失败");
                    if (apiResponse == null)
                        Console.WriteLine("   - apiResponse is null");
                    else if (!apiResponse.Success)
                        Console.WriteLine("   - apiResponse.Success is false");
                    else if (apiResponse.Data == null)
                        Console.WriteLine("   - apiResponse.Data is null");
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"无法连接到合约信息API ({_baseUrl})");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载合约信息时发生错误");
                return false;
            }
        }
        
        /// <summary>
        /// 规范化合约符号（去掉前缀和后缀）
        /// </summary>
        private string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return symbol;
            
            var normalized = symbol.ToUpper();
            
            // 1. 去掉1000000前缀（如 1000000PEPEUSDT -> PEPEUSDT）
            if (normalized.StartsWith("1000000"))
            {
                normalized = normalized.Substring(7); // 去掉 "1000000"
            }
            // 2. 去掉1000前缀（如 1000PEPEUSDT -> PEPEUSDT）
            else if (normalized.StartsWith("1000"))
            {
                normalized = normalized.Substring(4); // 去掉 "1000"
            }
            
            // 3. 去掉USDT/BUSD后缀（如 PEPEUSDT -> PEPE）
            normalized = normalized.Replace("USDT", "").Replace("BUSD", "");
            
            return normalized;
        }
        
        /// <summary>
        /// 尝试多种方式匹配合约
        /// </summary>
        private ContractInfo? TryMatchContract(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return null;
            
            var upperSymbol = symbol.ToUpper();
            
            // 方式1: 直接匹配（1000PEPEUSDT -> 1000PEPEUSDT）
            if (_contractCache.TryGetValue(upperSymbol, out var contractInfo))
                return contractInfo;
            
            // 方式2: 去掉USDT/BUSD后缀（1000PEPEUSDT -> 1000PEPE）
            var withoutSuffix = upperSymbol.Replace("USDT", "").Replace("BUSD", "");
            if (withoutSuffix != upperSymbol && _contractCache.TryGetValue(withoutSuffix, out contractInfo))
                return contractInfo;
            
            // 方式3: 去掉前缀和后缀（1000PEPEUSDT -> PEPE）
            var normalized = NormalizeSymbol(upperSymbol);
            if (normalized != upperSymbol && !string.IsNullOrEmpty(normalized) && _contractCache.TryGetValue(normalized, out contractInfo))
                return contractInfo;
            
            // 方式4: 只去掉前缀（1000PEPEUSDT -> PEPEUSDT）
            var withoutPrefix = upperSymbol;
            if (withoutPrefix.StartsWith("1000000"))
            {
                withoutPrefix = withoutPrefix.Substring(7);
                if (_contractCache.TryGetValue(withoutPrefix, out contractInfo))
                    return contractInfo;
            }
            else if (withoutPrefix.StartsWith("1000"))
            {
                withoutPrefix = withoutPrefix.Substring(4);
                if (_contractCache.TryGetValue(withoutPrefix, out contractInfo))
                    return contractInfo;
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取合约的流通市值（流通数量 × 当前价格）
        /// </summary>
        public decimal? GetCirculatingMarketCap(string symbol, decimal currentPrice)
        {
            if (string.IsNullOrEmpty(symbol) || currentPrice <= 0)
                return null;
            
            // 尝试多种方式匹配合约
            var contractInfo = TryMatchContract(symbol);
            
            if (contractInfo != null)
            {
                if (contractInfo.CirculatingSupply > 0)
                {
                    return contractInfo.CirculatingSupply * currentPrice;
                }
                else
                {
                    Console.WriteLine($"  ⚠️ {symbol}: 找到缓存但流通量为0 (CirculatingSupply={contractInfo.CirculatingSupply})");
                }
            }
            else
            {
                // 首次未找到时，输出缓存中的键示例（仅输出一次）
                if (_contractCache.Count > 0 && !_hasShownCacheKeySample)
                {
                    _hasShownCacheKeySample = true;
                    var cacheKeys = string.Join(", ", _contractCache.Keys.Take(5));
                    Console.WriteLine($"  ❌ {symbol}: 缓存中未找到");
                    Console.WriteLine($"     💡 缓存键示例(前5个): {cacheKeys}");
                    Console.WriteLine($"     💡 Ticker符号格式: {symbol.ToUpper()}");
                    Console.WriteLine($"     🔍 这表明API返回的合约符号格式与Binance ticker符号不匹配！");
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 获取合约信息
        /// </summary>
        public ContractInfo? GetContractInfo(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return null;
            
            var upperSymbol = symbol.ToUpper();
            
            // 尝试多种方式匹配合约
            var contractInfo = TryMatchContract(symbol);
            
            // 如果匹配成功，记录日志并返回
            if (contractInfo != null)
            {
                // 如果不是直接匹配成功的，记录转换日志
                if (!_contractCache.ContainsKey(upperSymbol) && !_loggedMissingSymbols.Contains(upperSymbol))
                {
                    _loggedMissingSymbols.Add(upperSymbol);
                    
                    // 找出实际匹配到的键
                    string matchedKey = _contractCache.FirstOrDefault(kvp => kvp.Value == contractInfo).Key ?? "未知";
                    Console.WriteLine($"✅ 合约匹配: {upperSymbol} -> {matchedKey}");
                }
                return contractInfo;
            }
            
            // 调试：首次查询失败时输出详细信息（每个缺失合约只输出一次）
            if (_contractCache.Count > 0 && !_loggedMissingSymbols.Contains(upperSymbol))
            {
                _loggedMissingSymbols.Add(upperSymbol);
                
                Console.WriteLine($"❌ 未找到合约: {upperSymbol}");
                
                // 只在前3个失败时显示缓存键示例
                if (_loggedMissingSymbols.Count <= 3)
                {
                    Console.WriteLine($"   📋 缓存中共有 {_contractCache.Count} 个合约");
                    Console.WriteLine($"   🔑 缓存键示例(前20个): {string.Join(", ", _contractCache.Keys.Take(20))}");
                    
                    // 显示包含BTC的所有缓存键
                    var btcKeys = _contractCache.Keys.Where(k => k.Contains("BTC")).ToList();
                    if (btcKeys.Any())
                    {
                        Console.WriteLine($"   🔍 缓存中包含BTC的合约: {string.Join(", ", btcKeys)}");
                    }
                    
                    // 显示包含ETH的所有缓存键
                    var ethKeys = _contractCache.Keys.Where(k => k.Contains("ETH")).ToList();
                    if (ethKeys.Any())
                    {
                        Console.WriteLine($"   🔍 缓存中包含ETH的合约: {string.Join(", ", ethKeys)}");
                    }
                }
                
                // 检查是否有相似的键
                var normalized = NormalizeSymbol(upperSymbol);
                var similarKeys = _contractCache.Keys
                    .Where(k => k.StartsWith(normalized) || k.Contains(normalized) || NormalizeSymbol(k) == normalized)
                    .ToList();
                
                if (similarKeys.Any())
                {
                    Console.WriteLine($"   💡 可能的相似合约: {string.Join(", ", similarKeys.Take(5))}");
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 是否已加载缓存
        /// </summary>
        public bool IsCacheLoaded => _contractCache.Count > 0;
        
        /// <summary>
        /// 缓存的合约数量
        /// </summary>
        public int CachedContractCount => _contractCache.Count;
        
        /// <summary>
        /// 上次加载时间
        /// </summary>
        public DateTime LastLoadTime => _lastLoadTime;
    }
} 