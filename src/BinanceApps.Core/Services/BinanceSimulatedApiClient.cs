using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 币安模拟API客户端实现
    /// 提供立即返回的模拟响应，用于开发和测试
    /// </summary>
    public class BinanceSimulatedApiClient : IBinanceSimulatedApiClient
    {
        private readonly SimulatedDataManager _dataManager;
        private string _apiKey = string.Empty;
        private string _secretKey = string.Empty;
        private bool _isTestnet = true;

        public BinanceSimulatedApiClient(SimulatedDataManager dataManager)
        {
            _dataManager = dataManager;
        }

        public string ApiKey { get => _apiKey; set => _apiKey = value; }
        public string SecretKey { get => _secretKey; set => _secretKey = value; }
        public bool IsTestnet { get => _isTestnet; set => _isTestnet = value; }

        public string BaseUrl => "https://simulated.binance.com";

        public async Task InitializeAsync(string apiKey, string secretKey, bool isTestnet = false)
        {
            ApiKey = apiKey;
            SecretKey = secretKey;
            IsTestnet = isTestnet;
            await Task.CompletedTask;
        }

        public async Task<bool> TestConnectionAsync()
        {
            await Task.CompletedTask;
            return true; // 模拟连接总是成功
        }

        public async Task<DateTime> GetServerTimeAsync()
        {
            await Task.CompletedTask;
            return DateTime.UtcNow;
        }

        public async Task<AccountInfo> GetAccountInfoAsync()
        {
            await Task.CompletedTask;
            var balances = _dataManager.GetBalances();
            var totalBalance = balances.Sum(b => b.TotalBalance);
            var availableBalance = balances.Sum(b => b.AvailableBalance);

            return new AccountInfo
            {
                AccountType = "UNIFIED",
                CanTrade = true,
                CanWithdraw = false,
                CanDeposit = false,
                UpdateTime = DateTime.UtcNow,
                TotalWalletBalance = totalBalance,
                TotalUnrealizedPnl = balances.Sum(b => b.UnrealizedPnl),
                TotalMarginBalance = balances.Sum(b => b.MarginBalance),
                TotalAvailableBalance = availableBalance
            };
        }

        public async Task<List<Balance>> GetAccountBalanceAsync()
        {
            await Task.CompletedTask;
            return _dataManager.GetBalances();
        }

        public async Task<List<Position>> GetPositionsAsync()
        {
            await Task.CompletedTask;
            return _dataManager.GetPositions();
        }

        public async Task<OrderResult> PlaceOrderAsync(PlaceOrderRequest order)
        {
            await Task.CompletedTask;

            try
            {
                // 验证订单
                if (string.IsNullOrEmpty(order.Symbol))
                {
                    return new OrderResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "交易对不能为空"
                    };
                }

                if (order.Quantity <= 0)
                {
                    return new OrderResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "数量必须大于0"
                    };
                }

                // 检查余额
                var usdtBalance = _dataManager.GetBalances().FirstOrDefault(b => b.Asset == "USDT");
                if (usdtBalance == null || usdtBalance.AvailableBalance < order.Quantity * order.Price)
                {
                    return new OrderResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "余额不足"
                    };
                }

                // 创建订单
                var newOrder = new BaseOrder
                {
                    Symbol = order.Symbol,
                    Side = order.Side,
                    OrderType = order.OrderType,
                    PositionSide = order.PositionSide,
                    Quantity = order.Quantity,
                    Price = order.Price,
                    TimeInForce = order.TimeInForce,
                    Status = OrderStatus.New,
                    ClientOrderId = order.ClientOrderId,
                    ExecutedQuantity = 0,
                    ExecutedQuoteQuantity = 0,
                    Commission = 0,
                    CommissionAsset = "USDT",
                    IsWorking = true
                };

                _dataManager.AddOrder(newOrder);

                // 冻结余额
                var requiredAmount = order.Quantity * order.Price;
                _dataManager.UpdateBalance("USDT", usdtBalance.AvailableBalance - requiredAmount, requiredAmount);

                return new OrderResult
                {
                    OrderId = newOrder.OrderId,
                    ClientOrderId = newOrder.ClientOrderId,
                    Symbol = newOrder.Symbol,
                    Status = newOrder.Status,
                    IsSuccess = true,
                    CreateTime = newOrder.CreateTime
                };
            }
            catch (Exception ex)
            {
                return new OrderResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<CancelOrderResult> CancelOrderAsync(string symbol, long orderId)
        {
            await Task.CompletedTask;

            try
            {
                var orders = _dataManager.GetOrders(symbol);
                var order = orders.FirstOrDefault(o => o.OrderId == orderId);

                if (order == null)
                {
                    return new CancelOrderResult
                    {
                        OrderId = orderId,
                        Symbol = symbol,
                        IsSuccess = false,
                        ErrorMessage = "订单不存在"
                    };
                }

                if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.Canceled)
                {
                    return new CancelOrderResult
                    {
                        OrderId = orderId,
                        Symbol = symbol,
                        IsSuccess = false,
                        ErrorMessage = "订单状态不允许取消"
                    };
                }

                // 更新订单状态
                order.Status = OrderStatus.Canceled;
                order.IsWorking = false;
                _dataManager.UpdateOrder(order);

                // 解冻余额
                var usdtBalance = _dataManager.GetBalances().FirstOrDefault(b => b.Asset == "USDT");
                if (usdtBalance != null)
                {
                    var frozenAmount = order.Quantity * order.Price;
                    _dataManager.UpdateBalance("USDT", usdtBalance.AvailableBalance + frozenAmount, usdtBalance.FrozenBalance - frozenAmount);
                }

                return new CancelOrderResult
                {
                    OrderId = orderId,
                    Symbol = symbol,
                    IsSuccess = true,
                    CancelTime = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new CancelOrderResult
                {
                    OrderId = orderId,
                    Symbol = symbol,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<BaseOrder> GetOrderAsync(string symbol, long orderId)
        {
            await Task.CompletedTask;
            var orders = _dataManager.GetOrders(symbol);
            return orders.FirstOrDefault(o => o.OrderId == orderId) ?? new BaseOrder();
        }

        public async Task<List<BaseOrder>> GetOrdersAsync(string symbol, int limit = 500)
        {
            await Task.CompletedTask;
            return _dataManager.GetOrders(symbol, limit);
        }

        public async Task<List<Kline>> GetKlinesAsync(string symbol, KlineInterval interval, int limit = 500)
        {
            await Task.CompletedTask;
            return _dataManager.GetKlines(symbol, interval, limit);
        }

        public async Task<PriceStatistics> Get24hrPriceStatisticsAsync(string symbol)
        {
            await Task.CompletedTask;
            var currentPrice = _dataManager.GetPrice(symbol);
            var prevPrice = currentPrice * 0.99m; // 模拟前一日价格

            return new PriceStatistics
            {
                Symbol = symbol,
                PriceChange = currentPrice - prevPrice,
                PriceChangePercent = (currentPrice - prevPrice) / prevPrice * 100,
                WeightedAvgPrice = currentPrice,
                PrevClosePrice = prevPrice,
                LastPrice = currentPrice,
                LastQty = 1,
                HighPrice = currentPrice * 1.02m,
                LowPrice = currentPrice * 0.98m,
                Volume = 1000,
                QuoteVolume = 1000 * currentPrice,
                OpenTime = DateTime.UtcNow.AddDays(-1),
                CloseTime = DateTime.UtcNow,
                FirstId = 1,
                LastId = 100,
                Count = 100
            };
        }

        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            await Task.CompletedTask;
            return _dataManager.GetPrice(symbol);
        }

        // 模拟API特有的方法
        public async Task ResetSimulatedAccountAsync(decimal initialBalance = 10000m)
        {
            await Task.CompletedTask;
            _dataManager.ResetAccount(initialBalance);
        }

        public async Task SetSimulatedPriceAsync(string symbol, decimal price)
        {
            await Task.CompletedTask;
            _dataManager.SetPrice(symbol, price);
        }

        public async Task<List<Balance>> GetSimulatedBalanceAsync()
        {
            await Task.CompletedTask;
            return _dataManager.GetBalances();
        }

        public async Task<List<Position>> GetSimulatedPositionsAsync()
        {
            await Task.CompletedTask;
            return _dataManager.GetPositions();
        }

        public async Task<List<BaseOrder>> GetSimulatedOrdersAsync(string symbol, int limit = 500)
        {
            await Task.CompletedTask;
            return _dataManager.GetOrders(symbol, limit);
        }

        public async Task<List<Kline>> GetSimulatedKlinesAsync(string symbol, KlineInterval interval, int limit = 500)
        {
            await Task.CompletedTask;
            return _dataManager.GetKlines(symbol, interval, limit);
        }

        /// <summary>
        /// 获取所有tick数据（24小时价格统计）
        /// </summary>
        public async Task<List<PriceStatistics>> GetAllTicksAsync()
        {
            await Task.CompletedTask;
            return _dataManager.GetAllTicks();
        }

        /// <summary>
        /// 获取所有可交易合约信息
        /// </summary>
        public async Task<List<SymbolInfo>> GetAllSymbolsInfoAsync()
        {
            await Task.CompletedTask;
            return _dataManager.GetAllSymbolsInfo();
        }

        /// <summary>
        /// 从本地文件加载合约信息
        /// </summary>
        public async Task<List<SymbolInfo>> LoadSymbolsFromFileAsync()
        {
            await Task.CompletedTask;
            return _dataManager.LoadSymbolsFromFile();
        }

        /// <summary>
        /// 保存合约信息到本地文件
        /// </summary>
        public async Task SaveSymbolsToFileAsync(List<SymbolInfo> symbols)
        {
            await Task.CompletedTask;
            _dataManager.SaveSymbolsToFile(symbols);
        }

        /// <summary>
        /// 从币安交易所获取永续合约tick信息
        /// 基于币安官方API文档：https://developers.binance.com/docs/zh-CN/binance-spot-api-docs/rest-api/market-data-endpoints#%E4%BA%A4%E6%98%93%E6%97%A5%E8%A1%8C%E6%83%85ticker
        /// </summary>
        public async Task<List<PriceStatistics>> GetBinancePerpetualTicksAsync()
        {
            await Task.CompletedTask;
            
            // 模拟从币安交易所获取数据
            // 这里应该调用真实的币安API，现在使用模拟数据
            var ticks = new List<PriceStatistics>();
            
            // 基于币安真实交易对，模拟获取500个左右的永续合约tick数据
            // 包含主流币种和一些小币种
            var baseSymbols = new[]
            {
                // 主流币种 (约50个) - 基于币安真实交易对
                "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "SOLUSDT", "DOTUSDT", "LINKUSDT", "LTCUSDT", "BCHUSDT", "XRPUSDT",
                "AVAXUSDT", "MATICUSDT", "UNIUSDT", "ATOMUSDT", "NEARUSDT", "FTMUSDT", "ALGOUSDT", "VETUSDT", "ICPUSDT", "FILUSDT",
                "DOGEUSDT", "SHIBUSDT", "TRXUSDT", "EOSUSDT", "XLMUSDT", "HBARUSDT", "THETAUSDT", "MANAUSDT", "SANDUSDT", "AXSUSDT",
                "CHZUSDT", "HOTUSDT", "ENJUSDT", "BATUSDT", "ZILUSDT", "IOTAUSDT", "NEOUSDT", "QTUMUSDT", "ZECUSDT", "DASHUSDT",
                "WAVESUSDT", "OMGUSDT", "KSMUSDT", "AAVEUSDT", "SNXUSDT", "COMPUSDT", "MKRUSDT", "YFIUSDT", "SUSHIUSDT", "CRVUSDT",
                "BALUSDT", "RENUSDT", "RSRUSDT", "STORJUSDT", "ANKRUSDT", "COTIUSDT", "1INCHUSDT", "ALPHAUSDT", "AUDIOUSDT", "BAKEUSDT"
            };
            
            // 生成更多币种，模拟500个左右的永续合约
            var allSymbols = new List<string>(baseSymbols);
            
            // 添加更多币种 (约450个) - 基于币安真实交易对
            var additionalCoins = new[]
            {
                "AAVE", "ADA", "ALGO", "ALPHA", "ANKR", "ANT", "AR", "ARPA", "AUDIO", "AVAX", "AXS", "BADGER", "BAKE", "BAL", "BAND",
                "BAT", "BCH", "BICO", "BNB", "BNT", "BOND", "BSV", "BTC", "BTS", "BTT", "BUSD", "CAKE", "CELO", "CELR", "CFX", "CHR",
                "CHZ", "CKB", "CLV", "COMP", "COS", "COTI", "CRV", "CTSI", "CTXC", "CVP", "CVX", "DASH", "DATA", "DCR", "DENT", "DGB",
                "DIA", "DOCK", "DODO", "DOGE", "DOT", "DYDX", "EGLD", "ELF", "ENJ", "ENS", "EOS", "ETC", "ETH", "FET", "FIL", "FLM",
                "FLOW", "FLR", "FOR", "FORTH", "FTM", "FTT", "FUN", "FXS", "GALA", "GTC", "HBAR", "HIVE", "HNT", "HOT", "ICP", "ICX",
                "IDEX", "IMX", "INJ", "IOST", "IOTA", "IOTX", "IRIS", "JASMY", "KAVA", "KDA", "KEY", "KLAY", "KMD", "KNC", "KSM", "LDO",
                "LINA", "LINK", "LIT", "LPT", "LQTY", "LRC", "LSK", "LTC", "LTO", "LUNA", "MAGIC", "MANA", "MASK", "MATIC", "MINA",
                "MKR", "MLN", "MOB", "MTL", "MULTI", "NANO", "NEAR", "NEO", "NKN", "NMR", "OCEAN", "OGN", "OM", "OMG", "ONE", "ONG",
                "ONT", "OP", "ORN", "OXT", "PAXG", "PEOPLE", "PERP", "PHA", "POLS", "POLYGON", "POND", "POWR", "PROM", "QNT", "QTUM",
                "RAD", "RARE", "RAY", "REEF", "REN", "REP", "REQ", "RLC", "ROSE", "RSR", "RUNE", "RVN", "SAND", "SCRT", "SFP", "SHIB",
                "SKL", "SLP", "SNX", "SOL", "SPELL", "SRM", "STARL", "STMX", "STORJ", "STPT", "STRAX", "STX", "SUPER", "SUSHI", "SXP",
                "SYN", "SYS", "T", "TFUEL", "THETA", "TLM", "TOKE", "TOMO", "TRB", "TRU", "TRX", "TVK", "TWT", "UMA", "UNI", "USDC",
                "USDP", "USDT", "UTK", "VET", "VGX", "VTHO", "WAVES", "WAXP", "WBTC", "WOO", "XEC", "XEM", "XLM", "XMR", "XRP", "XTZ",
                "YFI", "YGG", "ZEC", "ZEN", "ZIL", "ZRX"
            };
            
            // 为每个币种生成USDT交易对
            foreach (var coin in additionalCoins)
            {
                if (!allSymbols.Contains($"{coin}USDT"))
                {
                    allSymbols.Add($"{coin}USDT");
                }
            }
            
            // 限制到500个左右
            allSymbols = allSymbols.Take(500).ToList();
            
            var random = new Random();
            foreach (var symbol in allSymbols)
            {
                var basePrice = GetRealisticBasePrice(symbol.Replace("USDT", ""));
                var randomPrice = basePrice * (1 + (decimal)((random.NextDouble() - 0.5) * 0.1)); // 减少价格波动
                var volume = random.Next(10000, 10000000); // 更真实的交易量
                var priceChange = randomPrice * (decimal)((random.NextDouble() - 0.5) * 0.05); // 减少价格变化
                
                // 基于币安API文档的24hr ticker数据结构
                ticks.Add(new PriceStatistics
                {
                    Symbol = symbol,
                    LastPrice = randomPrice,
                    Volume = volume,
                    PriceChange = priceChange,
                    PriceChangePercent = (priceChange / basePrice) * 100,
                    HighPrice = randomPrice * 1.05m,
                    LowPrice = randomPrice * 0.95m,
                    OpenPrice = basePrice,
                    QuoteVolume = randomPrice * volume,
                    Count = random.Next(1000, 50000)
                });
            }
            
            return ticks;
        }

        /// <summary>
        /// 获取单个合约的详细信息
        /// 基于币安API文档：https://developers.binance.com/docs/zh-CN/binance-spot-api-docs/rest-api/market-data-endpoints
        /// </summary>
        public async Task<SymbolInfo> GetSymbolInfoAsync(string symbol)
        {
            await Task.CompletedTask;
            
            // 模拟从币安交易所获取单个合约信息
            // 在实际应用中，这里应该调用币安的 /api/v3/exchangeInfo 端点
            var baseAsset = symbol.Replace("USDT", "");
            
            return new SymbolInfo
            {
                Symbol = symbol,
                BaseAsset = baseAsset,
                QuoteAsset = "USDT",
                MinQty = GetMinQuantityFromManager(baseAsset),
                MaxQty = 1000000m,
                QtyPrecision = GetQuantityPrecisionFromManager(baseAsset),
                PricePrecision = GetPricePrecisionFromManager(baseAsset),
                MinPrice = 0.000001m,
                MaxPrice = 1000000m,
                MinNotional = 10m,
                IsTrading = true,
                ContractType = ContractType.Perpetual,
                ExpiryDate = null
            };
        }

        private decimal GetMinQuantityFromManager(string baseAsset)
        {
            return baseAsset switch
            {
                "BTC" => 0.001m,
                "ETH" => 0.01m,
                "BNB" => 0.01m,
                "ADA" => 1m,
                "SOL" => 0.1m,
                "DOT" => 0.1m,
                "LINK" => 0.1m,
                "LTC" => 0.01m,
                "BCH" => 0.01m,
                "XRP" => 1m,
                "AVAX" => 0.1m,
                "MATIC" => 1m,
                "UNI" => 0.1m,
                "ATOM" => 0.1m,
                "NEAR" => 0.1m,
                "FTM" => 1m,
                "ALGO" => 1m,
                "VET" => 10m,
                "ICP" => 0.1m,
                "FIL" => 0.1m,
                "DOGE" => 1000m,
                "SHIB" => 1000000m,
                "TRX" => 100m,
                "EOS" => 0.1m,
                "XLM" => 1m,
                _ => 0.001m
            };
        }

        private int GetQuantityPrecisionFromManager(string baseAsset)
        {
            return baseAsset switch
            {
                "BTC" => 3,
                "ETH" => 3,
                "BNB" => 3,
                "ADA" => 0,
                "SOL" => 1,
                "DOT" => 1,
                "LINK" => 1,
                "LTC" => 3,
                "BCH" => 3,
                "XRP" => 0,
                "AVAX" => 1,
                "MATIC" => 0,
                "UNI" => 1,
                "ATOM" => 1,
                "NEAR" => 1,
                "FTM" => 0,
                "ALGO" => 0,
                "VET" => 0,
                "ICP" => 1,
                "FIL" => 1,
                "DOGE" => 0,
                "SHIB" => 0,
                "TRX" => 0,
                "EOS" => 1,
                "XLM" => 0,
                _ => 3
            };
        }

        private int GetPricePrecisionFromManager(string baseAsset)
        {
            return baseAsset switch
            {
                "BTC" => 2,
                "ETH" => 2,
                "BNB" => 2,
                "ADA" => 4,
                "SOL" => 2,
                "DOT" => 3,
                "LINK" => 3,
                "LTC" => 2,
                "BCH" => 2,
                "XRP" => 4,
                "AVAX" => 2,
                "MATIC" => 4,
                "UNI" => 3,
                "ATOM" => 3,
                "NEAR" => 3,
                "FTM" => 4,
                "ALGO" => 4,
                "VET" => 5,
                "ICP" => 2,
                "FIL" => 3,
                "DOGE" => 5,
                "SHIB" => 8,
                "TRX" => 5,
                "EOS" => 3,
                "XLM" => 5,
                _ => 2
            };
        }

        private decimal GetRealisticBasePrice(string baseAsset)
        {
            return baseAsset switch
            {
                // 主流币种 - 更真实的价格
                "BTC" => 65000m,
                "ETH" => 3500m,
                "BNB" => 580m,
                "ADA" => 0.45m,
                "SOL" => 95m,
                "DOT" => 6.5m,
                "LINK" => 13m,
                "LTC" => 75m,
                "BCH" => 180m,
                "XRP" => 0.52m,
                "AVAX" => 28m,
                "MATIC" => 0.85m,
                "UNI" => 7.2m,
                "ATOM" => 8.5m,
                "NEAR" => 4.2m,
                "FTM" => 0.25m,
                "ALGO" => 0.18m,
                "VET" => 0.025m,
                "ICP" => 10.5m,
                "FIL" => 5.2m,
                "DOGE" => 0.085m,
                "SHIB" => 0.000008m,
                "TRX" => 0.095m,
                "EOS" => 0.85m,
                "XLM" => 0.18m,
                "HBAR" => 0.085m,
                "THETA" => 1.8m,
                "MANA" => 0.42m,
                "SAND" => 0.65m,
                "AXS" => 12.5m,
                "CHZ" => 0.18m,
                "HOT" => 0.0008m,
                "ENJ" => 0.35m,
                "BAT" => 0.28m,
                "ZIL" => 0.042m,
                "IOTA" => 0.25m,
                "NEO" => 18m,
                "QTUM" => 2.8m,
                "ZEC" => 95m,
                "DASH" => 140m,
                "WAVES" => 4.8m,
                "OMG" => 0.95m,
                "KSM" => 45m,
                "AAVE" => 180m,
                "SNX" => 8.5m,
                "COMP" => 135m,
                "MKR" => 1800m,
                "YFI" => 7200m,
                "SUSHI" => 1.8m,
                "CRV" => 0.85m,
                "BAL" => 18m,
                "REN" => 0.085m,
                "RSR" => 0.008m,
                "STORJ" => 0.45m,
                "ANKR" => 0.042m,
                "COTI" => 0.085m,
                "1INCH" => 0.45m,
                "ALPHA" => 0.18m,
                "AUDIO" => 0.25m,
                "BAKE" => 0.25m,
                "BICO" => 0.35m,
                "BOND" => 2.5m,
                "BTS" => 0.008m,
                "BTT" => 0.0000008m,
                "CAKE" => 2.8m,
                "CELO" => 0.65m,
                "CELR" => 0.008m,
                "CFX" => 0.18m,
                "CHR" => 0.18m,
                "CKB" => 0.008m,
                "CLV" => 0.18m,
                "COS" => 0.008m,
                "CTSI" => 0.18m,
                "CTXC" => 0.18m,
                "CVP" => 0.18m,
                "CVX" => 0.18m,
                "DATA" => 0.18m,
                "DCR" => 18m,
                "DENT" => 0.0008m,
                "DGB" => 0.008m,
                "DIA" => 0.18m,
                "DOCK" => 0.18m,
                "DODO" => 0.18m,
                "DYDX" => 1.8m,
                "EGLD" => 18m,
                "ELF" => 0.18m,
                "ENS" => 18m,
                "ETC" => 18m,
                "FET" => 0.18m,
                "FLM" => 0.18m,
                "FLOW" => 0.85m,
                "FLR" => 0.18m,
                "FOR" => 0.18m,
                "FORTH" => 0.18m,
                "FTT" => 0.18m,
                "FUN" => 0.008m,
                "FXS" => 0.18m,
                "GALA" => 0.018m,
                "GTC" => 0.18m,
                "HIVE" => 0.18m,
                "HNT" => 0.18m,
                "ICX" => 0.18m,
                "IDEX" => 0.18m,
                "IMX" => 0.18m,
                "INJ" => 0.18m,
                "IOST" => 0.008m,
                "IOTX" => 0.008m,
                "IRIS" => 0.008m,
                "JASMY" => 0.008m,
                "KAVA" => 0.85m,
                "KDA" => 0.85m,
                "KEY" => 0.008m,
                "KLAY" => 0.18m,
                "KMD" => 0.18m,
                "KNC" => 0.85m,
                "LDO" => 0.18m,
                "LINA" => 0.008m,
                "LIT" => 0.18m,
                "LPT" => 0.18m,
                "LQTY" => 0.18m,
                "LRC" => 0.18m,
                "LSK" => 0.18m,
                "LTO" => 0.18m,
                "LUNA" => 0.18m,
                "MAGIC" => 0.18m,
                "MASK" => 0.18m,
                "MINA" => 0.18m,
                "MLN" => 0.18m,
                "MOB" => 0.18m,
                "MTL" => 0.18m,
                "MULTI" => 0.18m,
                "NANO" => 0.18m,
                "NKN" => 0.18m,
                "NMR" => 0.18m,
                "OCEAN" => 0.18m,
                "OGN" => 0.18m,
                "OM" => 0.18m,
                "ONE" => 0.008m,
                "ONG" => 0.18m,
                "ONT" => 0.18m,
                "OP" => 0.18m,
                "ORN" => 0.18m,
                "OXT" => 0.18m,
                "PAXG" => 0.18m,
                "PEOPLE" => 0.008m,
                "PERP" => 0.18m,
                "PHA" => 0.18m,
                "POLS" => 0.18m,
                "POLYGON" => 0.18m,
                "POND" => 0.18m,
                "POWR" => 0.18m,
                "PROM" => 0.18m,
                "QNT" => 0.18m,
                "RAD" => 0.18m,
                "RARE" => 0.18m,
                "RAY" => 0.18m,
                "REEF" => 0.008m,
                "REP" => 0.18m,
                "REQ" => 0.18m,
                "RLC" => 0.18m,
                "ROSE" => 0.18m,
                "RUNE" => 0.18m,
                "RVN" => 0.008m,
                "SCRT" => 0.18m,
                "SFP" => 0.18m,
                "SKL" => 0.18m,
                "SLP" => 0.008m,
                "SPELL" => 0.008m,
                "SRM" => 0.18m,
                "STARL" => 0.008m,
                "STMX" => 0.008m,
                "STPT" => 0.18m,
                "STRAX" => 0.18m,
                "STX" => 0.18m,
                "SUPER" => 0.18m,
                "SXP" => 0.18m,
                "SYN" => 0.18m,
                "SYS" => 0.18m,
                "T" => 0.18m,
                "TFUEL" => 0.008m,
                "TLM" => 0.18m,
                "TOKE" => 0.18m,
                "TOMO" => 0.18m,
                "TRB" => 0.18m,
                "TRU" => 0.18m,
                "TVK" => 0.18m,
                "TWT" => 0.18m,
                "UMA" => 0.18m,
                "USDC" => 0.18m,
                "USDP" => 0.18m,
                "UTK" => 0.18m,
                "VGX" => 0.18m,
                "VTHO" => 0.008m,
                "WAXP" => 0.18m,
                "WBTC" => 0.18m,
                "WOO" => 0.18m,
                "XEC" => 0.008m,
                "XEM" => 0.18m,
                "XMR" => 0.18m,
                "XTZ" => 0.18m,
                "YGG" => 0.18m,
                "ZEN" => 0.18m,
                _ => 0.18m // 默认价格，更合理
            };
        }

        private decimal GetBasePrice(string baseAsset)
        {
            return GetRealisticBasePrice(baseAsset);
        }
    }
} 