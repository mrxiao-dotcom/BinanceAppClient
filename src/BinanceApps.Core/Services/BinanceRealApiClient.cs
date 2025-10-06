using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;
using System.Security.Cryptography;
using System.Linq;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 真实的币安API客户端
    /// 基于币安官方API文档：https://developers.binance.com/docs/zh-CN/binance-spot-api-docs/rest-api/market-data-endpoints
    /// </summary>
    public class BinanceRealApiClient : IBinanceSimulatedApiClient
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _secretKey;
        private bool _isTestnet;
        private readonly string _baseUrl;

        public string ApiKey { get => _apiKey; set => _apiKey = value; }
        public string SecretKey { get => _secretKey; set => _secretKey = value; }
        public bool IsTestnet { get => _isTestnet; set => _isTestnet = value; }
        public string BaseUrl => _baseUrl;

        public BinanceRealApiClient(string apiKey, string secretKey, bool isTestnet = false)
        {
            _apiKey = apiKey;
            _secretKey = secretKey;
            _isTestnet = isTestnet;
            _baseUrl = isTestnet ? "https://testnet.binance.vision" : "https://api.binance.com";
            
            // 创建支持系统代理的HttpClient
            var handler = new HttpClientHandler()
            {
                UseProxy = true,
                UseDefaultCredentials = true
            };
            
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(30); // 设置30秒超时
            
            Console.WriteLine("🌐 HttpClient已配置使用系统代理");
        }

        public async Task InitializeAsync(string apiKey, string secretKey, bool isTestnet)
        {
            _apiKey = apiKey;
            _secretKey = secretKey;
            _isTestnet = isTestnet;
            _httpClient.DefaultRequestHeaders.Remove("X-MBX-APIKEY");
            _httpClient.DefaultRequestHeaders.Add("X-MBX-APIKEY", _apiKey);
            await Task.CompletedTask;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                Console.WriteLine($"🔍 BinanceRealApiClient.TestConnectionAsync 开始");
                Console.WriteLine($"🔍 当前API Key: {_apiKey[..Math.Min(12, _apiKey.Length)]}...");
                Console.WriteLine($"🔍 当前Base URL: {_baseUrl}");
                
                // 先测试基本网络连接
                var pingResponse = await _httpClient.GetAsync($"{_baseUrl}/api/v3/ping");
                if (!pingResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ 网络连接失败：ping端点无响应");
                    System.Diagnostics.Debug.WriteLine("网络连接失败：ping端点无响应");
                    return false;
                }
                Console.WriteLine("✅ 网络连接成功");

                // 然后测试API Key有效性 - 使用测试订单端点（更安全、更可靠）
                try
                {
                    // 获取服务器时间以确保时间戳同步
                    DateTime serverTime;
                    try
                    {
                        serverTime = await GetServerTimeAsync();
                        Console.WriteLine($"🕐 获取到服务器时间: {serverTime:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                    catch
                    {
                        serverTime = DateTime.UtcNow;
                        Console.WriteLine($"⚠️ 使用本地时间: {serverTime:yyyy-MM-dd HH:mm:ss} UTC");
                    }
                    
                    // 使用测试订单端点验证API Key和签名
                    var timestamp = ((DateTimeOffset)serverTime).ToUnixTimeMilliseconds();
                    var timestampStr = GenerateSafeTimestampFromValue(timestamp);
                    
                    // 构建测试订单参数（不会真实下单）
                    var parameters = new Dictionary<string, string>
                    {
                        {"symbol", "BTCUSDT"},
                        {"side", "BUY"},
                        {"type", "LIMIT"},
                        {"timeInForce", "GTC"},
                        {"quantity", "0.001"},
                        {"price", "20000"},
                        {"timestamp", timestampStr},
                        {"recvWindow", "10000"}
                    };
                    
                    // 按字母顺序排序并构建查询字符串
                    var sortedParams = parameters.OrderBy(kv => kv.Key);
                    var queryString = string.Join("&", sortedParams.Select(kv => $"{kv.Key}={kv.Value}"));
                    var signature = GenerateSignature(queryString);
                    
                    Console.WriteLine($"🔍 请求时间戳: {timestampStr} ({serverTime:yyyy-MM-dd HH:mm:ss} UTC)");
                    Console.WriteLine($"🔍 测试订单参数: {queryString}");
                    Console.WriteLine($"🔍 签名: {signature[..Math.Min(20, signature.Length)]}...");
                    
                    // 验证时间戳格式
                    var isValidTimestamp = System.Text.RegularExpressions.Regex.IsMatch(timestampStr, @"^[0-9]{1,20}$");
                    Console.WriteLine($"🔍 时间戳格式验证: {(isValidTimestamp ? "✅ 有效" : "❌ 无效")}");
                    
                    // 发送测试订单请求
                    var testOrderUrl = $"{_baseUrl}/api/v3/order/test?{queryString}&signature={signature}";
                    Console.WriteLine($"🔍 测试订单URL: {testOrderUrl[..Math.Min(100, testOrderUrl.Length)]}...");
                    
                    var testOrderResponse = await _httpClient.PostAsync(testOrderUrl, null);
                    var testOrderContent = await testOrderResponse.Content.ReadAsStringAsync();
                    
                    if (testOrderResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("✅ API Key验证成功：测试订单通过验证");
                        System.Diagnostics.Debug.WriteLine("API Key验证成功：测试订单通过验证");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"❌ API Key验证失败：{testOrderResponse.StatusCode} - {testOrderContent}");
                        System.Diagnostics.Debug.WriteLine($"API Key验证失败：{testOrderResponse.StatusCode} - {testOrderContent}");
                        return false;
                    }
                }
                catch (Exception apiEx)
                {
                    Console.WriteLine($"❌ API Key验证异常：{apiEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"API Key验证异常：{apiEx.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"连接测试异常：{ex.Message}");
                return false;
            }
        }

        public async Task<DateTime> GetServerTimeAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/time");
            var content = await response.Content.ReadAsStringAsync();
            var timeResponse = JsonSerializer.Deserialize<BinanceTimeResponse>(content);
            return DateTimeOffset.FromUnixTimeMilliseconds(timeResponse?.ServerTime ?? 0).DateTime;
        }

        public async Task<AccountInfo> GetAccountInfoAsync()
        {
            // 获取服务器时间以避免时间戳错误
            var serverTime = await GetServerTimeAsync();
            var timestamp = ((DateTimeOffset)serverTime).ToUnixTimeMilliseconds();
            var timestampStr = GenerateSafeTimestampFromValue(timestamp);
            var queryString = $"timestamp={timestampStr}&recvWindow=10000";
            var signature = GenerateSignature(queryString);
            
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/account?{queryString}&signature={signature}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取账户信息失败: {content}");
            }
            
            var accountResponse = JsonSerializer.Deserialize<BinanceAccountResponse>(content);
            return new AccountInfo
            {
                AccountType = "UNIFIED",
                CanTrade = accountResponse?.CanTrade ?? false,
                CanWithdraw = accountResponse?.CanWithdraw ?? false,
                CanDeposit = accountResponse?.CanDeposit ?? false,
                TotalWalletBalance = accountResponse?.TotalWalletBalance ?? 0
            };
        }

        public async Task<List<Balance>> GetAccountBalanceAsync()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var queryString = $"timestamp={timestampStr}&recvWindow=10000";
            var signature = GenerateSignature(queryString);
            
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/account?{queryString}&signature={signature}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取余额失败: {content}");
            }
            
            var accountResponse = JsonSerializer.Deserialize<BinanceAccountResponse>(content);
            return accountResponse?.Balances
                ?.Where(b => decimal.Parse(b.Free) > 0 || decimal.Parse(b.Locked) > 0)
                ?.Select(b => new Balance
                {
                    Asset = b.Asset,
                    AvailableBalance = decimal.Parse(b.Free),
                    FrozenBalance = decimal.Parse(b.Locked)
                })
                ?.ToList() ?? new List<Balance>();
        }

        public Task<List<Position>> GetPositionsAsync()
        {
            // 现货API不支持持仓，返回空列表
            return Task.FromResult(new List<Position>());
        }

        public async Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest request)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var queryParams = new Dictionary<string, string>
            {
                ["symbol"] = request.Symbol,
                ["side"] = request.Side.ToString().ToUpper(),
                ["type"] = request.OrderType.ToString().ToUpper(),
                ["quantity"] = request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["timestamp"] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            if (request.OrderType == OrderType.Limit)
            {
                queryParams["price"] = request.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
                queryParams["timeInForce"] = request.TimeInForce.ToString().ToUpper();
            }

            if (!string.IsNullOrEmpty(request.ClientOrderId))
            {
                queryParams["newClientOrderId"] = request.ClientOrderId;
            }

            var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var signature = GenerateSignature(queryString);

            var postData = $"{queryString}&signature={signature}";
            var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/v3/order", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = JsonSerializer.Deserialize<BinanceErrorResponse>(responseContent);
                return new OrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = errorResponse?.Msg ?? "下单失败"
                };
            }

            var orderResponse = JsonSerializer.Deserialize<BinanceOrderResponse>(responseContent);
            return new OrderResult
            {
                IsSuccess = true,
                OrderId = orderResponse?.OrderId ?? 0,
                ClientOrderId = orderResponse?.ClientOrderId ?? "",
                Symbol = orderResponse?.Symbol ?? "",
                Status = ParseOrderStatus(orderResponse?.Status ?? ""),
                CreateTime = DateTime.UtcNow
            };
        }

        public async Task<CancelOrderResult> CancelOrderAsync(string symbol, long orderId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var queryString = $"symbol={symbol}&orderId={orderId}&timestamp={timestampStr}&recvWindow=10000";
            var signature = GenerateSignature(queryString);

            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/v3/order?{queryString}&signature={signature}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = JsonSerializer.Deserialize<BinanceErrorResponse>(content);
                return new CancelOrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = errorResponse?.Msg ?? "取消订单失败"
                };
            }

            var cancelResponse = JsonSerializer.Deserialize<BinanceOrderResponse>(content);
            return new CancelOrderResult
            {
                IsSuccess = true,
                OrderId = cancelResponse?.OrderId ?? 0,
                Symbol = cancelResponse?.Symbol ?? "",
                CancelTime = DateTime.UtcNow
            };
        }

        public async Task<BaseOrder> GetOrderAsync(string symbol, long orderId)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var queryString = $"symbol={symbol}&orderId={orderId}&timestamp={timestampStr}&recvWindow=10000";
            var signature = GenerateSignature(queryString);

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/order?{queryString}&signature={signature}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取订单失败: {content}");
            }

            var orderResponse = JsonSerializer.Deserialize<BinanceOrderResponse>(content);
            return new BaseOrder
            {
                OrderId = orderResponse?.OrderId ?? 0,
                Symbol = orderResponse?.Symbol ?? "",
                Side = ParseOrderSide(orderResponse?.Side ?? ""),
                OrderType = ParseOrderType(orderResponse?.Type ?? ""),
                Quantity = decimal.Parse(orderResponse?.OrigQty ?? "0"),
                Price = decimal.Parse(orderResponse?.Price ?? "0"),
                Status = ParseOrderStatus(orderResponse?.Status ?? ""),
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow
            };
        }

        public async Task<List<BaseOrder>> GetOrdersAsync(string symbol, int limit = 500)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var queryString = $"symbol={symbol}&timestamp={timestampStr}&recvWindow=10000";
            var signature = GenerateSignature(queryString);

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/openOrders?{queryString}&signature={signature}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取订单列表失败: {content}");
            }

            var ordersResponse = JsonSerializer.Deserialize<List<BinanceOrderResponse>>(content);
            return ordersResponse?.Take(limit).Select(o => new BaseOrder
            {
                OrderId = o.OrderId,
                Symbol = o.Symbol,
                Side = ParseOrderSide(o.Side),
                OrderType = ParseOrderType(o.Type),
                Quantity = decimal.Parse(o.OrigQty),
                Price = decimal.Parse(o.Price),
                Status = ParseOrderStatus(o.Status),
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow
            }).ToList() ?? new List<BaseOrder>();
        }

        public async Task<List<Kline>> GetKlinesAsync(string symbol, KlineInterval interval, int limit = 500)
        {
            var intervalString = GetBinanceIntervalString(interval);
            
            // 使用公开API获取K线数据，不需要API Key
            var apiUrl = _isTestnet ? "https://testnet.binancefuture.com/fapi/v1/klines" : "https://fapi.binance.com/fapi/v1/klines";
            var requestUrl = $"{apiUrl}?symbol={symbol}&interval={intervalString}&limit={limit}";
            
            System.Diagnostics.Debug.WriteLine($"正在获取K线数据: {requestUrl}");
            Console.WriteLine($"📈 正在获取 {symbol} 的K线数据: {requestUrl}");
            
            // 为公开API创建一个没有API Key的HttpClient
            using var publicHttpClient = new HttpClient();
            publicHttpClient.Timeout = TimeSpan.FromSeconds(30);
            publicHttpClient.DefaultRequestHeaders.Add("User-Agent", "BinanceApps/1.0");
            
            var response = await publicHttpClient.GetAsync(requestUrl);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取K线数据失败: {content}");
            }

            System.Diagnostics.Debug.WriteLine($"K线数据响应长度: {content.Length}");
            
            var klinesData = JsonSerializer.Deserialize<JsonElement[][]>(content);
            if (klinesData == null || klinesData.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine($"K线数据为空或解析失败: {symbol}");
                return new List<Kline>();
            }

            var klines = new List<Kline>();
            foreach (var k in klinesData)
            {
                try
                {
                    // 币安K线数据格式：[开盘时间, 开盘价, 最高价, 最低价, 收盘价, 成交量, 收盘时间, 成交额, 成交笔数, 主动买入成交量, 主动买入成交额, 忽略]
                    var kline = new Kline
                    {
                        OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(k[0].GetInt64()).UtcDateTime, // 使用UTC时间
                        OpenPrice = GetDecimalFromJsonElement(k[1]),
                        HighPrice = GetDecimalFromJsonElement(k[2]),
                        LowPrice = GetDecimalFromJsonElement(k[3]),
                        ClosePrice = GetDecimalFromJsonElement(k[4]),
                        Volume = GetDecimalFromJsonElement(k[5]), // 基础资产成交量
                        CloseTime = DateTimeOffset.FromUnixTimeMilliseconds(k[6].GetInt64()).UtcDateTime, // 使用UTC时间
                        QuoteVolume = GetDecimalFromJsonElement(k[7]), // USDT成交额
                        NumberOfTrades = k.Length > 8 ? k[8].GetInt32() : 0, // 成交笔数
                        TakerBuyVolume = k.Length > 9 ? GetDecimalFromJsonElement(k[9]) : 0m, // 主动买入成交量
                        TakerBuyQuoteVolume = k.Length > 10 ? GetDecimalFromJsonElement(k[10]) : 0m // 主动买入成交额
                    };
                    
                    klines.Add(kline);
                    
                    // 调试输出第一条K线数据
                    if (klines.Count == 1)
                    {
                        System.Diagnostics.Debug.WriteLine($"第一条K线数据 {symbol}:");
                        System.Diagnostics.Debug.WriteLine($"  时间: {kline.OpenTime:yyyy-MM-dd HH:mm:ss} UTC");
                        System.Diagnostics.Debug.WriteLine($"  开盘: {kline.OpenPrice:F8}");
                        System.Diagnostics.Debug.WriteLine($"  最高: {kline.HighPrice:F8}");
                        System.Diagnostics.Debug.WriteLine($"  最低: {kline.LowPrice:F8}");
                        System.Diagnostics.Debug.WriteLine($"  收盘: {kline.ClosePrice:F8}");
                        System.Diagnostics.Debug.WriteLine($"  成交量: {kline.Volume:F8}");
                        System.Diagnostics.Debug.WriteLine($"  USDT成交额: {kline.QuoteVolume:F2}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"解析K线数据失败 {symbol}: {ex.Message}");
                    continue;
                }
            }

            System.Diagnostics.Debug.WriteLine($"成功解析 {klines.Count} 条K线数据: {symbol}");
            return klines;
        }

        public async Task<PriceStatistics> Get24hrPriceStatisticsAsync(string symbol)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/ticker/24hr?symbol={symbol}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取24小时价格统计失败: {content}");
            }

            var statsResponse = JsonSerializer.Deserialize<Binance24hrTickerResponse>(content);
            return new PriceStatistics
            {
                Symbol = statsResponse?.Symbol ?? "",
                LastPrice = decimal.Parse(statsResponse?.LastPrice ?? "0"),
                Volume = decimal.Parse(statsResponse?.Volume ?? "0"),
                PriceChange = decimal.Parse(statsResponse?.PriceChange ?? "0"),
                PriceChangePercent = decimal.Parse(statsResponse?.PriceChangePercent ?? "0"),
                HighPrice = decimal.Parse(statsResponse?.HighPrice ?? "0"),
                LowPrice = decimal.Parse(statsResponse?.LowPrice ?? "0"),
                OpenPrice = decimal.Parse(statsResponse?.OpenPrice ?? "0"),
                QuoteVolume = decimal.Parse(statsResponse?.QuoteVolume ?? "0"),
                Count = statsResponse?.Count ?? 0
            };
        }

        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v3/ticker/price?symbol={symbol}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"获取最新价格失败: {content}");
            }

            var priceResponse = JsonSerializer.Deserialize<BinancePriceResponse>(content);
            return decimal.Parse(priceResponse?.Price ?? "0");
        }

        public async Task ResetSimulatedAccountAsync(decimal initialBalance)
        {
            // 真实API不支持重置账户
            await Task.CompletedTask;
        }

        public async Task<List<Balance>> GetSimulatedBalanceAsync()
        {
            return await GetAccountBalanceAsync();
        }

        public async Task SetSimulatedPriceAsync(string symbol, decimal price)
        {
            // 真实API不支持设置模拟价格
            await Task.CompletedTask;
        }

        public async Task<List<PriceStatistics>> GetAllTicksAsync()
        {
            try
            {
                // 使用公开API获取24小时价格统计，不需要API Key
                var futuresApiUrl = _isTestnet ? "https://testnet.binancefuture.com/fapi/v1/ticker/24hr" : "https://fapi.binance.com/fapi/v1/ticker/24hr";
                System.Diagnostics.Debug.WriteLine($"正在尝试访问币安永续合约API: {futuresApiUrl}");
                Console.WriteLine($"📊 正在调用公开API获取24小时价格统计: {futuresApiUrl}");
                
                // 为公开API创建一个没有API Key的HttpClient
                using var publicHttpClient = new HttpClient();
                publicHttpClient.Timeout = TimeSpan.FromSeconds(30);
                publicHttpClient.DefaultRequestHeaders.Add("User-Agent", "BinanceApps/1.0");
                
                var response = await publicHttpClient.GetAsync(futuresApiUrl);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"永续合约API调用成功，响应长度: {content.Length}");
                    
                    // 调试：查看原始JSON响应的前500个字符
                    var previewContent = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
                    System.Diagnostics.Debug.WriteLine($"JSON响应预览: {previewContent}");
                    
                    var allTickers = JsonSerializer.Deserialize<List<Binance24hrTickerResponse>>(content);
                    
                    // 调试：查看原始数据结构
                    System.Diagnostics.Debug.WriteLine($"总ticker数量: {allTickers?.Count ?? 0}");
                    if (allTickers?.Count > 0)
                    {
                        var sampleTicker = allTickers.First();
                        System.Diagnostics.Debug.WriteLine($"示例ticker: Symbol={sampleTicker.Symbol}");
                        
                        // 查看前几个ticker的信息
                        var firstFew = allTickers.Take(5).ToList();
                        foreach (var ticker in firstFew)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - {ticker.Symbol}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("反序列化失败，尝试手动解析JSON...");
                        // 尝试手动查找关键字段
                        if (content.Contains("symbol"))
                        {
                            System.Diagnostics.Debug.WriteLine("JSON包含'symbol'字段");
                        }
                        if (content.Contains("USDT"))
                        {
                            System.Diagnostics.Debug.WriteLine("JSON包含'USDT'字符串");
                        }
                    }
                    
                    // 只返回USDT永续合约 - 永续合约API中，USDT交易对通常以USDT结尾
                    var usdtTickers = allTickers?.Where(t => t.Symbol?.EndsWith("USDT") == true).ToList() ?? new List<Binance24hrTickerResponse>();
                    System.Diagnostics.Debug.WriteLine($"找到 {usdtTickers.Count} 个USDT永续合约");
                    
                    // 如果没有找到USDT交易对，尝试其他可能的命名规则
                    if (usdtTickers.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("尝试其他命名规则...");
                        // 永续合约可能使用不同的命名规则，比如BTCUSDT_PERP
                        var alternativeTickers = allTickers?.Where(t => 
                            (t.Symbol?.Contains("USDT") == true) || 
                            (t.Symbol?.EndsWith("USDT") == true)).ToList() ?? new List<Binance24hrTickerResponse>();
                        System.Diagnostics.Debug.WriteLine($"使用替代规则找到 {alternativeTickers.Count} 个交易对");
                        
                        if (alternativeTickers.Count > 0)
                        {
                            usdtTickers = alternativeTickers;
                        }
                    }
                    
                    return usdtTickers.Select(t => new PriceStatistics
                    {
                        Symbol = t.Symbol ?? "",
                        LastPrice = decimal.Parse(t.LastPrice ?? "0"),
                        Volume = decimal.Parse(t.Volume ?? "0"),
                        PriceChange = decimal.Parse(t.PriceChange ?? "0"),
                        PriceChangePercent = decimal.Parse(t.PriceChangePercent ?? "0"),
                        HighPrice = decimal.Parse(t.HighPrice ?? "0"),
                        LowPrice = decimal.Parse(t.LowPrice ?? "0"),
                        OpenPrice = decimal.Parse(t.OpenPrice ?? "0"),
                        QuoteVolume = decimal.Parse(t.QuoteVolume ?? "0"),
                        Count = t.Count
                    }).ToList();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"永续合约API调用失败，状态码: {response.StatusCode}, 响应: {content}");
                }
            }
            catch (Exception ex)
            {
                // 如果永续合约API失败，记录错误并回退到现货API
                System.Diagnostics.Debug.WriteLine($"永续合约API调用异常: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"内部异常: {ex.InnerException.Message}");
                }
            }

            // 回退到现货API
            System.Diagnostics.Debug.WriteLine("回退到现货API...");
            try
            {
                var spotResponse = await _httpClient.GetAsync($"{_baseUrl}/api/v3/ticker/24hr");
                var spotContent = await spotResponse.Content.ReadAsStringAsync();

                if (!spotResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"现货API调用失败，状态码: {spotResponse.StatusCode}, 响应: {spotContent}");
                    Console.WriteLine($"❌ 现货API调用失败:");
                    Console.WriteLine($"   🔍 状态码: {spotResponse.StatusCode}");
                    Console.WriteLine($"   📝 错误响应: {spotContent}");
                    Console.WriteLine();
                    return new List<PriceStatistics>(); // 返回空列表而不是抛出异常
                }

                System.Diagnostics.Debug.WriteLine($"现货API调用成功，响应长度: {spotContent.Length}");
                var spotTickers = JsonSerializer.Deserialize<List<Binance24hrTickerResponse>>(spotContent);
                
                // 只返回USDT交易对
                var usdtSpotTickers = spotTickers?.Where(t => t.Symbol?.EndsWith("USDT") == true).ToList() ?? new List<Binance24hrTickerResponse>();
                System.Diagnostics.Debug.WriteLine($"找到 {usdtSpotTickers.Count} 个USDT现货交易对");
                
                return usdtSpotTickers.Select(t => new PriceStatistics
                {
                    Symbol = t.Symbol ?? "",
                    LastPrice = decimal.Parse(t.LastPrice ?? "0"),
                    Volume = decimal.Parse(t.Volume ?? "0"),
                    PriceChange = decimal.Parse(t.PriceChange ?? "0"),
                    PriceChangePercent = decimal.Parse(t.PriceChangePercent ?? "0"),
                    HighPrice = decimal.Parse(t.HighPrice ?? "0"),
                    LowPrice = decimal.Parse(t.LowPrice ?? "0"),
                    OpenPrice = decimal.Parse(t.OpenPrice ?? "0"),
                    QuoteVolume = decimal.Parse(t.QuoteVolume ?? "0"),
                    Count = t.Count
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"现货API调用也失败: {ex.Message}");
                Console.WriteLine($"❌ 所有API调用都失败:");
                Console.WriteLine($"   🔍 错误类型: {ex.GetType().Name}");
                Console.WriteLine($"   📝 错误信息: {ex.Message}");
                Console.WriteLine($"   📍 错误位置: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
                Console.WriteLine();
                return new List<PriceStatistics>(); // 返回空列表而不是抛出异常
            }
        }

        public async Task<List<SymbolInfo>> GetAllSymbolsInfoAsync()
        {
            try
            {
                // 使用公开API，不需要API Key
                var futuresExchangeInfoUrl = _isTestnet ? "https://testnet.binancefuture.com/fapi/v1/exchangeInfo" : "https://fapi.binance.com/fapi/v1/exchangeInfo";
                System.Diagnostics.Debug.WriteLine($"正在尝试访问币安永续合约交易所信息API: {futuresExchangeInfoUrl}");
                Console.WriteLine($"🌐 正在调用公开API获取交易所信息: {futuresExchangeInfoUrl}");
                
                // 为公开API创建一个没有API Key的HttpClient
                using var publicHttpClient = new HttpClient();
                publicHttpClient.Timeout = TimeSpan.FromSeconds(30);
                publicHttpClient.DefaultRequestHeaders.Add("User-Agent", "BinanceApps/1.0");
                
                var response = await publicHttpClient.GetAsync(futuresExchangeInfoUrl);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"永续合约交易所信息API调用成功，响应长度: {content.Length}");
                    Console.WriteLine($"✅ 永续合约交易所信息API调用成功，响应长度: {content.Length}");
                    
                    // 调试：查看原始JSON响应的前500个字符
                    var previewContent = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
                    System.Diagnostics.Debug.WriteLine($"JSON响应预览: {previewContent}");
                    
                    var exchangeInfo = JsonSerializer.Deserialize<BinanceExchangeInfoResponse>(content);
                    
                    // 调试：查看原始数据结构
                    System.Diagnostics.Debug.WriteLine($"总交易对数量: {exchangeInfo?.Symbols?.Count ?? 0}");
                    Console.WriteLine($"📊 总交易对数量: {exchangeInfo?.Symbols?.Count ?? 0}");
                    
                    if (exchangeInfo?.Symbols?.Count > 0)
                    {
                        var sampleSymbol = exchangeInfo.Symbols.First();
                        System.Diagnostics.Debug.WriteLine($"示例交易对: Symbol={sampleSymbol.Symbol}, QuoteAsset={sampleSymbol.QuoteAsset}, Status={sampleSymbol.Status}");
                        Console.WriteLine($"📝 示例交易对: Symbol={sampleSymbol.Symbol}, QuoteAsset={sampleSymbol.QuoteAsset}, Status={sampleSymbol.Status}");
                        
                        // 查看前几个交易对的信息
                        var firstFew = exchangeInfo.Symbols.Take(5).ToList();
                        foreach (var symbol in firstFew)
                        {
                            System.Diagnostics.Debug.WriteLine($"  - {symbol.Symbol}: QuoteAsset={symbol.QuoteAsset}, Status={symbol.Status}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("反序列化失败，尝试手动解析JSON...");
                        // 尝试手动查找关键字段
                        if (content.Contains("symbols"))
                        {
                            System.Diagnostics.Debug.WriteLine("JSON包含'symbols'字段");
                        }
                        if (content.Contains("USDT"))
                        {
                            System.Diagnostics.Debug.WriteLine("JSON包含'USDT'字符串");
                        }
                    }
                    
                    // 只返回USDT永续合约
                    var usdtSymbols = exchangeInfo?.Symbols?.Where(s => s.QuoteAsset == "USDT" && s.Status == "TRADING").ToList() ?? new List<BinanceSymbol>();
                    System.Diagnostics.Debug.WriteLine($"找到 {usdtSymbols.Count} 个USDT永续合约交易对");
                    
                    // 如果没有找到USDT交易对，尝试其他可能的过滤条件
                    if (usdtSymbols.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("尝试其他过滤条件...");
                        
                        // 尝试不同的状态值
                        var tradingSymbols = exchangeInfo?.Symbols?.Where(s => s.Status == "TRADING").ToList() ?? new List<BinanceSymbol>();
                        System.Diagnostics.Debug.WriteLine($"TRADING状态的交易对数量: {tradingSymbols.Count}");
                        
                        // 尝试不同的计价资产字段
                        var allUsdtSymbols = exchangeInfo?.Symbols?.Where(s => 
                            (s.QuoteAsset == "USDT") || 
                            (s.Symbol?.EndsWith("USDT") == true)).ToList() ?? new List<BinanceSymbol>();
                        System.Diagnostics.Debug.WriteLine($"包含USDT的交易对数量: {allUsdtSymbols.Count}");
                        
                        // 如果还是找不到，使用所有TRADING状态的交易对
                        if (allUsdtSymbols.Count > 0)
                        {
                            usdtSymbols = allUsdtSymbols;
                        }
                        else if (tradingSymbols.Count > 0)
                        {
                            usdtSymbols = tradingSymbols;
                            System.Diagnostics.Debug.WriteLine("使用所有TRADING状态的交易对");
                        }
                    }
                    
                    return usdtSymbols.Select(s => new SymbolInfo
                    {
                        Symbol = s.Symbol ?? "",
                        BaseAsset = s.BaseAsset ?? "",
                        QuoteAsset = s.QuoteAsset ?? "",
                        MinQty = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "LOT_SIZE")?.MinQty, out var minQty) ? minQty : 0m,
                        MaxQty = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "LOT_SIZE")?.MaxQty, out var maxQty) ? maxQty : 1000000m,
                        QtyPrecision = GetPrecisionFromStepSize(s.Filters?.FirstOrDefault(f => f.FilterType == "LOT_SIZE")?.StepSize),
                        PricePrecision = s.QuotePrecision,
                        MinPrice = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "PRICE_FILTER")?.MinPrice, out var minPrice) ? minPrice : 0.000001m,
                        MaxPrice = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "PRICE_FILTER")?.MaxPrice, out var maxPrice) ? maxPrice : 1000000m,
                        MinNotional = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "MIN_NOTIONAL")?.MinNotional, out var minNotional) ? minNotional : 10m,
                        IsTrading = s.Status == "TRADING",
                        ContractType = ContractType.Perpetual, // 永续合约
                        ExpiryDate = null
                    }).ToList();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"永续合约交易所信息API调用失败，状态码: {response.StatusCode}, 响应: {content}");
                    Console.WriteLine($"❌ 永续合约交易所信息API调用失败: {response.StatusCode}");
                    Console.WriteLine($"   🌐 请求URL: {futuresExchangeInfoUrl}");
                    Console.WriteLine($"   📝 错误内容: {content}");
                }
            }
            catch (Exception ex)
            {
                // 如果永续合约API失败，记录错误并回退到现货API
                System.Diagnostics.Debug.WriteLine($"永续合约交易所信息API调用异常: {ex.Message}");
                Console.WriteLine($"❌ 永续合约交易所信息API调用异常: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"内部异常: {ex.InnerException.Message}");
                    Console.WriteLine($"   🔍 内部异常: {ex.InnerException.Message}");
                }
            }

            // 回退到现货API
            System.Diagnostics.Debug.WriteLine("回退到现货交易所信息API...");
            try
            {
                var spotResponse = await _httpClient.GetAsync($"{_baseUrl}/api/v3/exchangeInfo");
                var spotContent = await spotResponse.Content.ReadAsStringAsync();

                if (!spotResponse.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"现货交易所信息API调用失败，状态码: {spotResponse.StatusCode}, 响应: {spotContent}");
                    Console.WriteLine($"❌ 现货交易所信息API调用失败:");
                    Console.WriteLine($"   🔍 状态码: {spotResponse.StatusCode}");
                    Console.WriteLine($"   📝 错误响应: {spotContent}");
                    Console.WriteLine();
                    return new List<SymbolInfo>(); // 返回空列表而不是抛出异常
                }

                System.Diagnostics.Debug.WriteLine($"现货交易所信息API调用成功，响应长度: {spotContent.Length}");
                var spotExchangeInfo = JsonSerializer.Deserialize<BinanceExchangeInfoResponse>(spotContent);
                
                                // 只返回USDT交易对
                var usdtSpotSymbols = spotExchangeInfo?.Symbols?.Where(s => s.QuoteAsset == "USDT" && s.Status == "TRADING").ToList() ?? new List<BinanceSymbol>();
                System.Diagnostics.Debug.WriteLine($"找到 {usdtSpotSymbols.Count} 个USDT现货交易对");
                
                return usdtSpotSymbols.Select(s => new SymbolInfo
                {
                    Symbol = s.Symbol ?? "",
                    BaseAsset = s.BaseAsset ?? "",
                    QuoteAsset = s.QuoteAsset ?? "",
                    MinQty = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "LOT_SIZE")?.MinQty, out var minQty) ? minQty : 0m,
                    MaxQty = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "LOT_SIZE")?.MaxQty, out var maxQty) ? maxQty : 1000000m,
                    QtyPrecision = GetPrecisionFromStepSize(s.Filters?.FirstOrDefault(f => f.FilterType == "LOT_SIZE")?.StepSize),
                    PricePrecision = s.QuotePrecision,
                    MinPrice = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "PRICE_FILTER")?.MinPrice, out var minPrice) ? minPrice : 0.000001m,
                    MaxPrice = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "PRICE_FILTER")?.MaxPrice, out var maxPrice) ? maxPrice : 1000000m,
                    MinNotional = decimal.TryParse(s.Filters?.FirstOrDefault(f => f.FilterType == "MIN_NOTIONAL")?.MinNotional, out var minNotional) ? minNotional : 10m,
                    IsTrading = s.Status == "TRADING",
                    ContractType = ContractType.Perpetual, // 永续合约
                    ExpiryDate = null
                }).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"现货交易所信息API调用也失败: {ex.Message}");
                Console.WriteLine($"❌ 所有交易所信息API调用都失败:");
                Console.WriteLine($"   🔍 错误类型: {ex.GetType().Name}");
                Console.WriteLine($"   📝 错误信息: {ex.Message}");
                Console.WriteLine($"   📍 错误位置: {ex.StackTrace?.Split('\n').FirstOrDefault()}");
                Console.WriteLine();
                return new List<SymbolInfo>(); // 返回空列表而不是抛出异常
            }
        }

        public async Task<List<SymbolInfo>> LoadSymbolsFromFileAsync()
        {
            // 真实API不支持从文件加载
            return await GetAllSymbolsInfoAsync();
        }

        public async Task SaveSymbolsToFileAsync(List<SymbolInfo> symbols)
        {
            // 真实API不支持保存到文件
            await Task.CompletedTask;
        }

        public async Task<List<PriceStatistics>> GetBinancePerpetualTicksAsync()
        {
            // 使用现货API获取所有USDT交易对的24小时数据
            return await GetAllTicksAsync();
        }

        public async Task<SymbolInfo> GetSymbolInfoAsync(string symbol)
        {
            var allSymbols = await GetAllSymbolsInfoAsync();
            return allSymbols.FirstOrDefault(s => s.Symbol == symbol) ?? new SymbolInfo
            {
                Symbol = symbol,
                BaseAsset = symbol.Replace("USDT", ""),
                QuoteAsset = "USDT",
                MinQty = 0.000001m,
                MaxQty = 1000000m,
                QtyPrecision = 6,
                PricePrecision = 8,
                MinPrice = 0.000001m,
                MaxPrice = 1000000m,
                MinNotional = 10m,
                IsTrading = true,
                ContractType = ContractType.Perpetual,
                ExpiryDate = null
            };
        }

        // 实现IBinanceSimulatedApiClient的模拟方法
        public Task<List<Position>> GetSimulatedPositionsAsync()
        {
            // 现货API不支持持仓，返回空列表
            return Task.FromResult(new List<Position>());
        }

        public async Task<List<BaseOrder>> GetSimulatedOrdersAsync(string symbol, int limit = 500)
        {
            // 使用真实API获取订单
            return await GetOrdersAsync(symbol, limit);
        }

        public async Task<List<Kline>> GetSimulatedKlinesAsync(string symbol, KlineInterval interval, int limit = 500)
        {
            // 使用真实API获取K线数据
            return await GetKlinesAsync(symbol, interval, limit);
        }

        /// <summary>
        /// 生成安全的时间戳字符串，使用服务器时间避免时间同步问题
        /// </summary>
        /// <returns>纯数字格式的时间戳字符串</returns>
        private async Task<string> GenerateSafeTimestampAsync()
        {
            try
            {
                // 优先使用服务器时间
                var serverTime = await GetServerTimeAsync();
                var timestamp = ((DateTimeOffset)serverTime).ToUnixTimeMilliseconds();
                var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
                
                Console.WriteLine($"🕐 使用服务器时间生成时间戳: {timestampStr} ({serverTime:yyyy-MM-dd HH:mm:ss} UTC)");
                return timestampStr;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 获取服务器时间失败，使用本地时间: {ex.Message}");
                // 如果获取服务器时间失败，使用本地时间
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
                
                // 验证时间戳格式，确保只包含数字
                var isValid = System.Text.RegularExpressions.Regex.IsMatch(timestampStr, @"^[0-9]{1,20}$");
                if (!isValid)
                {
                    Console.WriteLine($"⚠️ 时间戳格式异常: '{timestampStr}'，尝试修复...");
                    timestampStr = System.Text.RegularExpressions.Regex.Replace(timestampStr, @"[^0-9]", "");
                    Console.WriteLine($"✅ 修复后的时间戳: '{timestampStr}'");
                }
                
                return timestampStr;
            }
        }
        
        /// <summary>
        /// 生成安全的时间戳字符串（同步版本，用于向后兼容）
        /// </summary>
        /// <returns>纯数字格式的时间戳字符串</returns>
        private string GenerateSafeTimestamp()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return GenerateSafeTimestampFromValue(timestamp);
        }
        
        /// <summary>
        /// 从时间戳值生成安全的字符串格式
        /// </summary>
        /// <param name="timestamp">时间戳值</param>
        /// <returns>纯数字格式的时间戳字符串</returns>
        private string GenerateSafeTimestampFromValue(long timestamp)
        {
            var timestampStr = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture);
            
            // 强制清理所有非数字字符
            timestampStr = System.Text.RegularExpressions.Regex.Replace(timestampStr, @"[^0-9]", "");
            
            // 验证最终格式
            var isValid = System.Text.RegularExpressions.Regex.IsMatch(timestampStr, @"^[0-9]{1,20}$");
            if (!isValid)
            {
                Console.WriteLine($"⚠️ 时间戳格式仍然异常: '{timestampStr}'，使用备用方案");
                // 备用方案：直接转换为字符串并手动清理
                timestampStr = timestamp.ToString();
                timestampStr = new string(timestampStr.Where(char.IsDigit).ToArray());
            }
            
            Console.WriteLine($"🕐 生成安全时间戳: {timestampStr} (长度: {timestampStr.Length})");
            return timestampStr;
        }

        private string GenerateSignature(string queryString)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return Convert.ToHexString(hash).ToLower();
        }

        private OrderSide ParseOrderSide(string side) => side?.ToUpper() switch
        {
            "BUY" => OrderSide.Buy,
            "SELL" => OrderSide.Sell,
            _ => OrderSide.Buy
        };

        private OrderType ParseOrderType(string type) => type?.ToUpper() switch
        {
            "LIMIT" => OrderType.Limit,
            "MARKET" => OrderType.Market,
            "STOP" => OrderType.Stop,
            "STOP_LIMIT" => OrderType.StopLimit,
            "TRAILING_STOP" => OrderType.TrailingStop,
            "ICEBERG" => OrderType.Iceberg,
            _ => OrderType.Limit
        };

        private OrderStatus ParseOrderStatus(string status) => status?.ToUpper() switch
        {
            "NEW" => OrderStatus.New,
            "PARTIALLY_FILLED" => OrderStatus.PartiallyFilled,
            "FILLED" => OrderStatus.Filled,
            "CANCELED" => OrderStatus.Canceled,
            "REJECTED" => OrderStatus.Rejected,
            "EXPIRED" => OrderStatus.Expired,
            _ => OrderStatus.New
        };

        /// <summary>
        /// 安全地从StepSize字符串计算精度
        /// </summary>
        private int GetPrecisionFromStepSize(string? stepSize)
        {
            if (string.IsNullOrEmpty(stepSize))
                return 8;

            if (decimal.TryParse(stepSize, out var step))
            {
                var stepStr = step.ToString();
                var dotIndex = stepStr.IndexOf('.');
                if (dotIndex >= 0 && dotIndex < stepStr.Length - 1)
                {
                    return stepStr.Length - dotIndex - 1;
                }
                return 0; // 整数，精度为0
            }

            return 8; // 默认精度
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        /// <summary>
        /// 将KlineInterval枚举转换为Binance API期望的字符串值
        /// </summary>
        private static string GetBinanceIntervalString(KlineInterval interval)
        {
            return interval switch
            {
                KlineInterval.OneMinute => "1m",
                KlineInterval.ThreeMinutes => "3m",
                KlineInterval.FiveMinutes => "5m",
                KlineInterval.FifteenMinutes => "15m",
                KlineInterval.ThirtyMinutes => "30m",
                KlineInterval.OneHour => "1h",
                KlineInterval.TwoHours => "2h",
                KlineInterval.FourHours => "4h",
                KlineInterval.SixHours => "6h",
                KlineInterval.EightHours => "8h",
                KlineInterval.TwelveHours => "12h",
                KlineInterval.OneDay => "1d",
                KlineInterval.ThreeDays => "3d",
                KlineInterval.OneWeek => "1w",
                KlineInterval.OneMonth => "1M",
                _ => "1d" // 默认使用1天
            };
        }

        /// <summary>
        /// 安全地从JsonElement获取decimal值，支持字符串和数字类型
        /// </summary>
        private static decimal GetDecimalFromJsonElement(JsonElement element)
        {
            try
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.String:
                        return decimal.Parse(element.GetString() ?? "0");
                    case JsonValueKind.Number:
                        return element.GetDecimal();
                    default:
                        return 0m;
                }
            }
            catch
            {
                return 0m;
            }
        }
    }

    // 币安API响应模型
    public class BinanceTimeResponse
    {
        public long ServerTime { get; set; }
    }

    public class BinanceAccountResponse
    {
        public bool CanTrade { get; set; }
        public bool CanWithdraw { get; set; }
        public bool CanDeposit { get; set; }
        public decimal TotalWalletBalance { get; set; }
        public List<BinanceBalance> Balances { get; set; } = new();
    }

    public class BinanceBalance
    {
        public string Asset { get; set; } = "";
        public string Free { get; set; } = "0";
        public string Locked { get; set; } = "0";
    }

    public class BinanceOrderResponse
    {
        public long OrderId { get; set; }
        public string Symbol { get; set; } = "";
        public string Side { get; set; } = "";
        public string Type { get; set; } = "";
        public string OrigQty { get; set; } = "0";
        public string Price { get; set; } = "0";
        public string Status { get; set; } = "";
        public string ClientOrderId { get; set; } = "";
    }

    public class BinanceErrorResponse
    {
        public string Msg { get; set; } = "";
    }

    public class Binance24hrTickerResponse
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = "";
        [JsonPropertyName("lastPrice")]
        public string LastPrice { get; set; } = "0";
        [JsonPropertyName("volume")]
        public string Volume { get; set; } = "0";
        [JsonPropertyName("priceChange")]
        public string PriceChange { get; set; } = "0";
        [JsonPropertyName("priceChangePercent")]
        public string PriceChangePercent { get; set; } = "0";
        [JsonPropertyName("highPrice")]
        public string HighPrice { get; set; } = "0";
        [JsonPropertyName("lowPrice")]
        public string LowPrice { get; set; } = "0";
        [JsonPropertyName("openPrice")]
        public string OpenPrice { get; set; } = "0";
        [JsonPropertyName("quoteVolume")]
        public string QuoteVolume { get; set; } = "0";
        [JsonPropertyName("count")]
        public long Count { get; set; }
    }

    public class BinancePriceResponse
    {
        public string Symbol { get; set; } = "";
        public string Price { get; set; } = "0";
    }

    public class BinanceExchangeInfoResponse
    {
        [JsonPropertyName("symbols")]
        public List<BinanceSymbol> Symbols { get; set; } = new();
    }

    public class BinanceSymbol
    {
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = "";
        [JsonPropertyName("baseAsset")]
        public string BaseAsset { get; set; } = "";
        [JsonPropertyName("quoteAsset")]
        public string QuoteAsset { get; set; } = "";
        [JsonPropertyName("quotePrecision")]
        public int QuotePrecision { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
        [JsonPropertyName("filters")]
        public List<BinanceFilter> Filters { get; set; } = new();
    }

    public class BinanceFilter
    {
        [JsonPropertyName("filterType")]
        public string FilterType { get; set; } = "";
        [JsonPropertyName("minQty")]
        public string MinQty { get; set; } = "0";
        [JsonPropertyName("maxQty")]
        public string MaxQty { get; set; } = "1000000";
        [JsonPropertyName("stepSize")]
        public string StepSize { get; set; } = "0.00000001";
        [JsonPropertyName("minPrice")]
        public string MinPrice { get; set; } = "0.000001";
        [JsonPropertyName("maxPrice")]
        public string MaxPrice { get; set; } = "1000000";
        [JsonPropertyName("minNotional")]
        public string MinNotional { get; set; } = "10";
    }

} 