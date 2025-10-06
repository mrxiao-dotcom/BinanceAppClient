using Xunit;
using BinanceApps.Core.Services;
using BinanceApps.Core.Models;

namespace BinanceApps.Core.Tests
{
    public class SimulatedDataManagerTests
    {
        private readonly SimulatedDataManager _dataManager;
        private readonly string _testDataDir;

        public SimulatedDataManagerTests()
        {
            _testDataDir = Path.Combine(Path.GetTempPath(), "SimulatedDataTests", Guid.NewGuid().ToString());
            _dataManager = new SimulatedDataManager(_testDataDir);
        }

        [Fact]
        public void Constructor_ShouldInitializeWithDefaultData()
        {
            // Act
            var balances = _dataManager.GetBalances();
            var prices = _dataManager.GetPrice("BTCUSDT");

            // Assert
            Assert.Single(balances);
            Assert.Equal("USDT", balances[0].Asset);
            Assert.Equal(10000m, balances[0].AvailableBalance);
            Assert.Equal(50000m, prices);
        }

        [Fact]
        public void ResetAccount_ShouldResetToInitialBalance()
        {
            // Arrange
            var initialBalance = 50000m;

            // Act
            _dataManager.ResetAccount(initialBalance);
            var balances = _dataManager.GetBalances();

            // Assert
            Assert.Single(balances);
            Assert.Equal("USDT", balances[0].Asset);
            Assert.Equal(initialBalance, balances[0].AvailableBalance);
            Assert.Empty(_dataManager.GetPositions());
            Assert.Empty(_dataManager.GetOrders());
        }

        [Fact]
        public void SetPrice_ShouldUpdatePrice()
        {
            // Arrange
            var symbol = "BTCUSDT";
            var newPrice = 60000m;

            // Act
            _dataManager.SetPrice(symbol, newPrice);
            var price = _dataManager.GetPrice(symbol);

            // Assert
            Assert.Equal(newPrice, price);
        }

        [Fact]
        public void GetPrice_ShouldReturnDefaultPriceForUnknownSymbol()
        {
            // Arrange
            var unknownSymbol = "UNKNOWNUSDT";

            // Act
            var price = _dataManager.GetPrice(unknownSymbol);

            // Assert
            Assert.Equal(100m, price);
        }

        [Fact]
        public void AddOrder_ShouldCreateOrderWithValidId()
        {
            // Arrange
            var order = new BaseOrder
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            // Act
            _dataManager.AddOrder(order);
            var orders = _dataManager.GetOrders();

            // Assert
            Assert.Single(orders);
            Assert.Equal(1, orders[0].OrderId);
            Assert.Equal("BTCUSDT", orders[0].Symbol);
            Assert.Equal(OrderSide.Buy, orders[0].Side);
        }

        [Fact]
        public void UpdateOrder_ShouldUpdateExistingOrder()
        {
            // Arrange
            var order = new BaseOrder
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            _dataManager.AddOrder(order);
            order.Status = OrderStatus.Filled;

            // Act
            _dataManager.UpdateOrder(order);
            var updatedOrder = _dataManager.GetOrders().First();

            // Assert
            Assert.Equal(OrderStatus.Filled, updatedOrder.Status);
        }

        [Fact]
        public void AddPosition_ShouldCreateNewPosition()
        {
            // Arrange
            var position = new Position
            {
                Symbol = "BTCUSDT",
                PositionSide = PositionSide.Long,
                PositionAmt = 1,
                PositionValue = 50000m,
                EntryPrice = 50000m,
                MarkPrice = 50000m,
                Leverage = 10
            };

            // Act
            _dataManager.AddPosition(position);
            var positions = _dataManager.GetPositions();

            // Assert
            Assert.Single(positions);
            Assert.Equal("BTCUSDT", positions[0].Symbol);
            Assert.Equal(PositionSide.Long, positions[0].PositionSide);
            Assert.Equal(1, positions[0].PositionAmt);
        }

        [Fact]
        public void UpdateBalance_ShouldUpdateExistingBalance()
        {
            // Arrange
            var asset = "USDT";
            var newAvailableBalance = 15000m;
            var newFrozenBalance = 5000m;

            // Act
            _dataManager.UpdateBalance(asset, newAvailableBalance, newFrozenBalance);
            var balances = _dataManager.GetBalances();
            var usdtBalance = balances.First(b => b.Asset == asset);

            // Assert
            Assert.Equal(newAvailableBalance, usdtBalance.AvailableBalance);
            Assert.Equal(newFrozenBalance, usdtBalance.FrozenBalance);
            Assert.Equal(newAvailableBalance + newFrozenBalance, usdtBalance.TotalBalance);
        }

        [Fact]
        public void UpdateBalance_ShouldCreateNewBalanceForUnknownAsset()
        {
            // Arrange
            var asset = "ETH";
            var availableBalance = 1000m;

            // Act
            _dataManager.UpdateBalance(asset, availableBalance);
            var balances = _dataManager.GetBalances();
            var ethBalance = balances.FirstOrDefault(b => b.Asset == asset);

            // Assert
            Assert.NotNull(ethBalance);
            Assert.Equal(asset, ethBalance.Asset);
            Assert.Equal(availableBalance, ethBalance.AvailableBalance);
        }

        [Fact]
        public void GetKlines_ShouldReturnGeneratedKlines()
        {
            // Arrange
            var symbol = "BTCUSDT";
            var interval = KlineInterval.OneHour;
            var limit = 10;

            // Act
            var klines = _dataManager.GetKlines(symbol, interval, limit);

            // Assert
            Assert.NotNull(klines);
            Assert.True(klines.Count > 0);
            Assert.True(klines.Count <= limit);
            Assert.All(klines, k => Assert.Equal(symbol, k.Symbol));
        }

        [Fact]
        public void GetOrders_ShouldFilterBySymbol()
        {
            // Arrange
            var order1 = new BaseOrder
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            var order2 = new BaseOrder
            {
                Symbol = "ETHUSDT",
                Side = OrderSide.Sell,
                OrderType = OrderType.Market,
                PositionSide = PositionSide.Short,
                Quantity = 10,
                Price = 3000m,
                TimeInForce = TimeInForce.IOC
            };

            _dataManager.AddOrder(order1);
            _dataManager.AddOrder(order2);

            // Act
            var btcOrders = _dataManager.GetOrders("BTCUSDT");
            var ethOrders = _dataManager.GetOrders("ETHUSDT");

            // Assert
            Assert.Single(btcOrders);
            Assert.Equal("BTCUSDT", btcOrders[0].Symbol);
            Assert.Single(ethOrders);
            Assert.Equal("ETHUSDT", ethOrders[0].Symbol);
        }

        [Fact]
        public void GetOrders_ShouldRespectLimit()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                var order = new BaseOrder
                {
                    Symbol = "BTCUSDT",
                    Side = OrderSide.Buy,
                    OrderType = OrderType.Limit,
                    PositionSide = PositionSide.Long,
                    Quantity = 1,
                    Price = 50000m + i,
                    TimeInForce = TimeInForce.GTC
                };
                _dataManager.AddOrder(order);
            }

            // Act
            var orders = _dataManager.GetOrders("BTCUSDT", 10);

            // Assert
            Assert.Equal(10, orders.Count);
        }

        private void Dispose()
        {
            // Clean up test data directory
            if (Directory.Exists(_testDataDir))
            {
                Directory.Delete(_testDataDir, true);
            }
        }
    }
} 