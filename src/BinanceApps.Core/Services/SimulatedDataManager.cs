using System.Text.Json;
using BinanceApps.Core.Models;

namespace BinanceApps.Core.Services
{
    /// <summary>
    /// 模拟数据管理器
    /// 负责管理模拟账户、持仓、订单等数据
    /// </summary>
    public class SimulatedDataManager
    {
        private readonly string _dataDirectory;
        private readonly string _balanceFile;
        private readonly string _positionsFile;
        private readonly string _ordersFile;
        private readonly string _pricesFile;
        private readonly string _klinesFile;

        private List<Models.Balance> _balances;
        private List<Models.Position> _positions;
        private List<Models.BaseOrder> _orders;
        private Dictionary<string, decimal> _prices;
        private Dictionary<string, List<Models.Kline>> _klines;

        public SimulatedDataManager(string dataDirectory = "SimulatedData")
        {
            _dataDirectory = dataDirectory;
            _balanceFile = Path.Combine(_dataDirectory, "balances.json");
            _positionsFile = Path.Combine(_dataDirectory, "positions.json");
            _ordersFile = Path.Combine(_dataDirectory, "orders.json");
            _pricesFile = Path.Combine(_dataDirectory, "prices.json");
            _klinesFile = Path.Combine(_dataDirectory, "klines.json");

            _balances = new List<Balance>();
            _positions = new List<Position>();
            _orders = new List<BaseOrder>();
            _prices = new Dictionary<string, decimal>();
            _klines = new Dictionary<string, List<Kline>>();

            InitializeDataDirectory();
            LoadData();
        }

        /// <summary>
        /// 初始化数据目录
        /// </summary>
        private void InitializeDataDirectory()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            try
            {
                if (File.Exists(_balanceFile))
                    _balances = JsonSerializer.Deserialize<List<Balance>>(File.ReadAllText(_balanceFile)) ?? new List<Balance>();
                
                if (File.Exists(_positionsFile))
                    _positions = JsonSerializer.Deserialize<List<Position>>(File.ReadAllText(_positionsFile)) ?? new List<Position>();
                
                if (File.Exists(_ordersFile))
                    _orders = JsonSerializer.Deserialize<List<BaseOrder>>(File.ReadAllText(_ordersFile)) ?? new List<BaseOrder>();
                
                if (File.Exists(_pricesFile))
                    _prices = JsonSerializer.Deserialize<Dictionary<string, decimal>>(File.ReadAllText(_pricesFile)) ?? new Dictionary<string, decimal>();
                
                if (File.Exists(_klinesFile))
                    _klines = JsonSerializer.Deserialize<Dictionary<string, List<Kline>>>(File.ReadAllText(_klinesFile)) ?? new Dictionary<string, List<Kline>>();
            }
            catch
            {
                // 如果加载失败，使用默认数据
                InitializeDefaultData();
            }
        }

        /// <summary>
        /// 初始化默认数据
        /// </summary>
        private void InitializeDefaultData()
        {
            // 默认USDT余额
            _balances = new List<Balance>
            {
                new Balance
                {
                    Asset = "USDT",
                    AvailableBalance = 10000m,
                    TotalBalance = 10000m,
                    FrozenBalance = 0m,
                    WalletBalance = 10000m,
                    UnrealizedPnl = 0m,
                    MarginBalance = 10000m,
                    UpdateTime = DateTime.UtcNow
                }
            };

            // 默认价格
            _prices = new Dictionary<string, decimal>
            {
                { "BTCUSDT", 50000m },
                { "ETHUSDT", 3000m },
                { "BNBUSDT", 400m }
            };

            // 生成默认K线数据
            GenerateDefaultKlines();
        }

        /// <summary>
        /// 生成默认K线数据
        /// </summary>
        private void GenerateDefaultKlines()
        {
            var symbols = new[] { "BTCUSDT", "ETHUSDT", "BNBUSDT" };
            var intervals = new[] { KlineInterval.OneMinute, KlineInterval.FiveMinutes, KlineInterval.OneHour, KlineInterval.OneDay };

            foreach (var symbol in symbols)
            {
                if (!_klines.ContainsKey(symbol))
                    _klines[symbol] = new List<Kline>();

                foreach (var interval in intervals)
                {
                    var key = $"{symbol}_{interval}";
                    if (!_klines.ContainsKey(key))
                    {
                        _klines[key] = GenerateKlines(symbol, interval, 100);
                    }
                }
            }
        }

        /// <summary>
        /// 生成K线数据
        /// </summary>
        private List<Kline> GenerateKlines(string symbol, KlineInterval interval, int count)
        {
            var klines = new List<Kline>();
            var basePrice = _prices.GetValueOrDefault(symbol, 100m);
            var currentTime = DateTime.UtcNow.AddDays(-count);

            for (int i = 0; i < count; i++)
            {
                var random = new Random((int)(currentTime.Ticks + i));
                var priceChange = (decimal)((random.NextDouble() - 0.5) * (double)basePrice * 0.1);
                var openPrice = basePrice + priceChange;
                var highPrice = openPrice + (decimal)(random.NextDouble() * (double)basePrice * 0.05);
                var lowPrice = openPrice - (decimal)(random.NextDouble() * (double)basePrice * 0.05);
                var closePrice = openPrice + (decimal)((random.NextDouble() - 0.5) * (double)basePrice * 0.02);
                var volume = (decimal)(random.NextDouble() * 1000 + 100);

                klines.Add(new Kline
                {
                    OpenTime = currentTime,
                    OpenPrice = openPrice,
                    HighPrice = highPrice,
                    LowPrice = lowPrice,
                    ClosePrice = closePrice,
                    Volume = volume,
                    CloseTime = currentTime.AddMinutes(GetIntervalMinutes(interval)),
                    QuoteVolume = volume * (openPrice + closePrice) / 2,
                    NumberOfTrades = random.Next(100, 1000),
                    TakerBuyVolume = volume * (decimal)(random.NextDouble() * 0.6 + 0.2),
                    TakerBuyQuoteVolume = volume * (openPrice + closePrice) / 2 * (decimal)(random.NextDouble() * 0.6 + 0.2)
                });

                currentTime = currentTime.AddMinutes(GetIntervalMinutes(interval));
            }

            return klines;
        }

        /// <summary>
        /// 获取时间间隔对应的分钟数
        /// </summary>
        private int GetIntervalMinutes(KlineInterval interval)
        {
            return interval switch
            {
                KlineInterval.OneMinute => 1,
                KlineInterval.ThreeMinutes => 3,
                KlineInterval.FiveMinutes => 5,
                KlineInterval.FifteenMinutes => 15,
                KlineInterval.ThirtyMinutes => 30,
                KlineInterval.OneHour => 60,
                KlineInterval.TwoHours => 120,
                KlineInterval.FourHours => 240,
                KlineInterval.SixHours => 360,
                KlineInterval.EightHours => 480,
                KlineInterval.TwelveHours => 720,
                KlineInterval.OneDay => 1440,
                KlineInterval.ThreeDays => 4320,
                KlineInterval.OneWeek => 10080,
                KlineInterval.OneMonth => 43200,
                _ => 1
            };
        }

        /// <summary>
        /// 保存数据
        /// </summary>
        private void SaveData()
        {
            try
            {
                File.WriteAllText(_balanceFile, JsonSerializer.Serialize(_balances, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(_positionsFile, JsonSerializer.Serialize(_positions, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(_ordersFile, JsonSerializer.Serialize(_orders, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(_pricesFile, JsonSerializer.Serialize(_prices, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(_klinesFile, JsonSerializer.Serialize(_klines, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常
                Console.WriteLine($"保存模拟数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置账户数据
        /// </summary>
        public void ResetAccount(decimal initialBalance = 10000m)
        {
            _balances = new List<Balance>
            {
                new Balance
                {
                    Asset = "USDT",
                    AvailableBalance = initialBalance,
                    TotalBalance = initialBalance,
                    FrozenBalance = 0m,
                    WalletBalance = initialBalance,
                    UnrealizedPnl = 0m,
                    MarginBalance = initialBalance,
                    UpdateTime = DateTime.UtcNow
                }
            };

            _positions.Clear();
            _orders.Clear();
            SaveData();
        }

        /// <summary>
        /// 设置价格
        /// </summary>
        public void SetPrice(string symbol, decimal price)
        {
            _prices[symbol] = price;
            SaveData();
        }

        /// <summary>
        /// 获取价格
        /// </summary>
        public decimal GetPrice(string symbol)
        {
            return _prices.GetValueOrDefault(symbol, 100m);
        }

        /// <summary>
        /// 获取所有tick数据（24小时价格统计）
        /// </summary>
        public List<PriceStatistics> GetAllTicks()
        {
            var ticks = new List<PriceStatistics>();
            var random = new Random();
            var currentTime = DateTime.UtcNow;
            
            // 生成更多的永续合约tick数据
            var symbols = new[]
            {
                "BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT", "SOLUSDT",
                "DOTUSDT", "LINKUSDT", "LTCUSDT", "BCHUSDT", "XRPUSDT",
                "AVAXUSDT", "MATICUSDT", "UNIUSDT", "ATOMUSDT", "NEARUSDT",
                "FTMUSDT", "ALGOUSDT", "VETUSDT", "ICPUSDT", "FILUSDT",
                "DOGEUSDT", "SHIBUSDT", "TRXUSDT", "EOSUSDT", "XLMUSDT",
                "BATUSDT", "ZECUSDT", "DASHUSDT", "XMRUSDT", "ETCUSDT",
                "NEOUSDT", "QTUMUSDT", "IOTAUSDT", "VTHOUSDT", "OMGUSDT",
                "ZRXUSDT", "REPUSDT", "KNCUSDT", "BANDUSDT", "COMPUSDT",
                "MKRUSDT", "YFIUSDT", "SNXUSDT", "AAVEUSDT", "SUSHIUSDT",
                "CRVUSDT", "1INCHUSDT", "ENJUSDT", "MANAUSDT", "SANDUSDT",
                "GALAUSDT", "AXSUSDT", "CHZUSDT", "HOTUSDT", "ANKRUSDT",
                "ZILUSDT", "IOTXUSDT", "RVNUSDT", "HIVEUSDT", "STXUSDT",
                "ARUSDT", "STORJUSDT", "SKLUSDT", "ALPHAUSDT", "AUDIOUSDT",
                "OCEANUSDT", "RENUSDT", "RSRUSDT", "CTSIUSDT", "ANKRUSDT",
                "BICOUSDT", "JASMYUSDT", "PEOPLEUSDT", "ENSUSDT", "IMXUSDT",
                "GMTUSDT", "APEUSDT", "OPUSDT", "ARBUSDT", "INJUSDT",
                "TIAUSDT", "SEIUSDT", "SUIUSDT", "BLURUSDT", "MASKUSDT",
                "LDOUSDT", "UNISWAPUSDT", "PENDLEUSDT", "JUPUSDT", "WIFUSDT",
                "BONKUSDT", "FLOKIUSDT", "PEPEUSDT", "WLDUSDT", "ORDIUSDT",
                "MEMEUSDT", "BIGTIMEUSDT", "PYTHUSDT", "JTOUSDT", "BONKUSDT",
                "MYROUSDT", "POPCATUSDT", "BOOKUSDT", "SMOGUSDT", "WENUSDT"
            };

            foreach (var symbol in symbols)
            {
                var baseAsset = symbol.Replace("USDT", "");
                var basePrice = _prices.GetValueOrDefault(symbol, 100m);
                var randomChange = (decimal)((random.NextDouble() - 0.5) * 0.2); // ±10% 变化
                var currentPrice = basePrice * (1 + randomChange);
                var prevPrice = currentPrice / (1 + randomChange);
                
                ticks.Add(new PriceStatistics
                {
                    Symbol = symbol,
                    PriceChange = currentPrice - prevPrice,
                    PriceChangePercent = randomChange * 100,
                    WeightedAvgPrice = currentPrice,
                    PrevClosePrice = prevPrice,
                    LastPrice = currentPrice,
                    LastQty = random.Next(1, 1000),
                    HighPrice = currentPrice * (1 + (decimal)(random.NextDouble() * 0.1)),
                    LowPrice = currentPrice * (1 - (decimal)(random.NextDouble() * 0.1)),
                    Volume = random.Next(1000, 1000000),
                    QuoteVolume = random.Next(1000, 1000000) * currentPrice,
                    OpenTime = currentTime.AddDays(-1),
                    CloseTime = currentTime,
                    FirstId = random.Next(1, 1000),
                    LastId = random.Next(1000, 2000),
                    Count = random.Next(100, 1000)
                });
            }
            
            return ticks;
        }

        /// <summary>
        /// 获取所有可交易合约信息
        /// </summary>
        public List<SymbolInfo> GetAllSymbolsInfo()
        {
            var symbols = new List<SymbolInfo>();
            
            // 定义一些模拟不可交易的合约（用于测试过滤功能）
            var nonTradingSymbols = new HashSet<string>
            {
                "LUNAUSDT", "USTUSDT", "TFUELUSDT", "XECUSDT", "SCRTUSDT", 
                "MOBUSDT", "PAXGUSDT", "USDPUSDT", "VTDUSUSDT"
            };
            
            // 获取所有tick数据中的交易对
            var ticks = GetAllTicks();
            
            foreach (var tick in ticks)
            {
                var baseAsset = tick.Symbol.Replace("USDT", "");
                var basePrice = _prices.GetValueOrDefault(tick.Symbol, 100m);
                
                // 检查是否为模拟的不可交易合约
                var isTrading = !nonTradingSymbols.Contains(tick.Symbol);
                
                symbols.Add(new SymbolInfo
                {
                    Symbol = tick.Symbol,
                    BaseAsset = baseAsset,
                    QuoteAsset = "USDT",
                    MinQty = GetMinQuantity(baseAsset),
                    MaxQty = 1000000m,
                    QtyPrecision = GetQuantityPrecision(baseAsset),
                    PricePrecision = GetPricePrecision(baseAsset),
                    MinPrice = 0.000001m,
                    MaxPrice = 1000000m,
                    MinNotional = 10m,
                    IsTrading = isTrading, // 根据列表设置交易状态
                    ContractType = ContractType.Perpetual,
                    ExpiryDate = null
                });
            }
            
            return symbols;
        }

        /// <summary>
        /// 从本地文件加载合约信息
        /// </summary>
        public List<SymbolInfo> LoadSymbolsFromFile()
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, "coininfo.json");
                if (File.Exists(filePath))
                {
                    var jsonContent = File.ReadAllText(filePath);
                    var symbols = System.Text.Json.JsonSerializer.Deserialize<List<SymbolInfo>>(jsonContent);
                    return symbols ?? new List<SymbolInfo>();
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，返回空列表
                System.Diagnostics.Debug.WriteLine($"加载本地合约信息失败: {ex.Message}");
            }
            
            return new List<SymbolInfo>();
        }

        /// <summary>
        /// 保存合约信息到本地文件
        /// </summary>
        public void SaveSymbolsToFile(List<SymbolInfo> symbols)
        {
            try
            {
                var filePath = Path.Combine(_dataDirectory, "coininfo.json");
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(symbols, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(filePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存合约信息到本地文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取最小数量
        /// </summary>
        private decimal GetMinQuantity(string baseAsset)
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

        /// <summary>
        /// 获取数量精度
        /// </summary>
        private int GetQuantityPrecision(string baseAsset)
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

        /// <summary>
        /// 获取价格精度
        /// </summary>
        private int GetPricePrecision(string baseAsset)
        {
            return baseAsset switch
            {
                "BTC" => 2,
                "ETH" => 2,
                "BNB" => 2,
                "ADA" => 4,
                "SOL" => 3,
                "DOT" => 3,
                "LINK" => 3,
                "LTC" => 2,
                "BCH" => 2,
                "XRP" => 4,
                "AVAX" => 3,
                "MATIC" => 4,
                "UNI" => 3,
                "ATOM" => 3,
                "NEAR" => 3,
                "FTM" => 4,
                "ALGO" => 4,
                "VET" => 5,
                "ICP" => 3,
                "FIL" => 3,
                "DOGE" => 6,
                "SHIB" => 8,
                "TRX" => 5,
                "EOS" => 3,
                "XLM" => 5,
                _ => 2
            };
        }

        /// <summary>
        /// 获取余额
        /// </summary>
        public List<Balance> GetBalances()
        {
            return _balances.ToList();
        }

        /// <summary>
        /// 获取持仓
        /// </summary>
        public List<Position> GetPositions()
        {
            return _positions.ToList();
        }

        /// <summary>
        /// 获取订单
        /// </summary>
        public List<BaseOrder> GetOrders(string symbol = "", int limit = 500)
        {
            var orders = _orders.AsEnumerable();
            
            if (!string.IsNullOrEmpty(symbol))
                orders = orders.Where(o => o.Symbol == symbol);
            
            return orders.Take(limit).ToList();
        }

        /// <summary>
        /// 获取K线数据
        /// </summary>
        public List<Kline> GetKlines(string symbol, KlineInterval interval, int limit = 500)
        {
            var key = $"{symbol}_{interval}";
            if (_klines.ContainsKey(key))
            {
                return _klines[key].Take(limit).ToList();
            }
            return new List<Kline>();
        }

        /// <summary>
        /// 添加订单
        /// </summary>
        public void AddOrder(BaseOrder order)
        {
            order.OrderId = _orders.Count > 0 ? _orders.Max(o => o.OrderId) + 1 : 1;
            order.CreateTime = DateTime.UtcNow;
            order.UpdateTime = DateTime.UtcNow;
            _orders.Add(order);
            SaveData();
        }

        /// <summary>
        /// 更新订单
        /// </summary>
        public void UpdateOrder(BaseOrder order)
        {
            var existingOrder = _orders.FirstOrDefault(o => o.OrderId == order.OrderId);
            if (existingOrder != null)
            {
                var index = _orders.IndexOf(existingOrder);
                order.UpdateTime = DateTime.UtcNow;
                _orders[index] = order;
                SaveData();
            }
        }

        /// <summary>
        /// 添加持仓
        /// </summary>
        public void AddPosition(Models.Position position)
        {
            var existingPosition = _positions.FirstOrDefault(p => p.Symbol == position.Symbol && p.PositionSide == position.PositionSide);
            if (existingPosition != null)
            {
                var index = _positions.IndexOf(existingPosition);
                _positions[index] = position;
            }
            else
            {
                _positions.Add(position);
            }
            SaveData();
        }

        /// <summary>
        /// 更新余额
        /// </summary>
        public void UpdateBalance(string asset, decimal availableBalance, decimal frozenBalance = 0)
        {
            var balance = _balances.FirstOrDefault(b => b.Asset == asset);
            if (balance != null)
            {
                balance.AvailableBalance = availableBalance;
                balance.FrozenBalance = frozenBalance;
                balance.TotalBalance = availableBalance + frozenBalance;
                balance.UpdateTime = DateTime.UtcNow;
            }
            else
            {
                _balances.Add(new Balance
                {
                    Asset = asset,
                    AvailableBalance = availableBalance,
                    TotalBalance = availableBalance + frozenBalance,
                    FrozenBalance = frozenBalance,
                    WalletBalance = availableBalance + frozenBalance,
                    UnrealizedPnl = 0m,
                    MarginBalance = availableBalance + frozenBalance,
                    UpdateTime = DateTime.UtcNow
                });
            }
            SaveData();
        }
    }
} 