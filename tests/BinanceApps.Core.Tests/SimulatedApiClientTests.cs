using Xunit;
using BinanceApps.Core.Services;
using BinanceApps.Core.Interfaces;
using BinanceApps.Core.Models;

namespace BinanceApps.Core.Tests
{
    public class SimulatedApiClientTests
    {
        private readonly SimulatedDataManager _dataManager;
        private readonly IBinanceSimulatedApiClient _apiClient;
        private readonly string _testDataDir;

        public SimulatedApiClientTests()
        {
            _testDataDir = Path.Combine(Path.GetTempPath(), "SimulatedApiTests", Guid.NewGuid().ToString());
            _dataManager = new SimulatedDataManager(_testDataDir);
            _apiClient = new BinanceSimulatedApiClient(_dataManager);
        }

        [Fact]
        public async Task InitializeAsync_ShouldSetApiCredentials()
        {
            // Arrange
            var apiKey = "test_api_key";
            var secretKey = "test_secret_key";
            var isTestnet = true;

            // Act
            await _apiClient.InitializeAsync(apiKey, secretKey, isTestnet);

            // Assert
            Assert.Equal(apiKey, _apiClient.ApiKey);
            Assert.Equal(secretKey, _apiClient.SecretKey);
            Assert.Equal(isTestnet, _apiClient.IsTestnet);
        }

        [Fact]
        public async Task TestConnectionAsync_ShouldAlwaysReturnTrue()
        {
            // Act
            var result = await _apiClient.TestConnectionAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task GetServerTimeAsync_ShouldReturnCurrentTime()
        {
            // Arrange
            var beforeCall = DateTime.UtcNow;

            // Act
            var serverTime = await _apiClient.GetServerTimeAsync();
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.True(serverTime >= beforeCall && serverTime <= afterCall);
        }

        [Fact]
        public async Task GetAccountInfoAsync_ShouldReturnValidAccountInfo()
        {
            // Act
            var accountInfo = await _apiClient.GetAccountInfoAsync();

            // Assert
            Assert.NotNull(accountInfo);
            Assert.Equal("UNIFIED", accountInfo.AccountType);
            Assert.True(accountInfo.CanTrade);
            Assert.False(accountInfo.CanWithdraw);
            Assert.False(accountInfo.CanDeposit);
            Assert.Equal(10000m, accountInfo.TotalWalletBalance);
        }

        [Fact]
        public async Task GetAccountBalanceAsync_ShouldReturnBalances()
        {
            // Act
            var balances = await _apiClient.GetAccountBalanceAsync();

            // Assert
            Assert.NotNull(balances);
            Assert.Single(balances);
            Assert.Equal("USDT", balances[0].Asset);
            Assert.Equal(10000m, balances[0].AvailableBalance);
        }

        [Fact]
        public async Task GetPositionsAsync_ShouldReturnEmptyPositions()
        {
            // Act
            var positions = await _apiClient.GetPositionsAsync();

            // Assert
            Assert.NotNull(positions);
            Assert.Empty(positions);
        }

        [Fact]
        public async Task PlaceOrderAsync_ShouldCreateValidOrder()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC,
                ClientOrderId = "test_client_id"
            };

            // Act
            var result = await _apiClient.PlaceOrderAsync(orderRequest);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("test_client_id", result.ClientOrderId);
            Assert.Equal("BTCUSDT", result.Symbol);
            Assert.Equal(OrderStatus.New, result.Status);
            Assert.True(result.OrderId > 0);
        }

        [Fact]
        public async Task PlaceOrderAsync_ShouldValidateSymbol()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            // Act
            var result = await _apiClient.PlaceOrderAsync(orderRequest);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("交易对不能为空", result.ErrorMessage);
        }

        [Fact]
        public async Task PlaceOrderAsync_ShouldValidateQuantity()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 0,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            // Act
            var result = await _apiClient.PlaceOrderAsync(orderRequest);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("数量必须大于0", result.ErrorMessage);
        }

        [Fact]
        public async Task PlaceOrderAsync_ShouldCheckBalance()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1000, // 需要5000万USDT
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            // Act
            var result = await _apiClient.PlaceOrderAsync(orderRequest);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("余额不足", result.ErrorMessage);
        }

        [Fact]
        public async Task CancelOrderAsync_ShouldCancelExistingOrder()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            var placeResult = await _apiClient.PlaceOrderAsync(orderRequest);
            Assert.True(placeResult.IsSuccess);

            // Act
            var cancelResult = await _apiClient.CancelOrderAsync("BTCUSDT", placeResult.OrderId);

            // Assert
            Assert.True(cancelResult.IsSuccess);
            Assert.Equal(placeResult.OrderId, cancelResult.OrderId);
            Assert.Equal("BTCUSDT", cancelResult.Symbol);
        }

        [Fact]
        public async Task CancelOrderAsync_ShouldHandleNonExistentOrder()
        {
            // Act
            var result = await _apiClient.CancelOrderAsync("BTCUSDT", 99999);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("订单不存在", result.ErrorMessage);
        }

        [Fact]
        public async Task GetOrderAsync_ShouldReturnOrder()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            var placeResult = await _apiClient.PlaceOrderAsync(orderRequest);

            // Act
            var order = await _apiClient.GetOrderAsync("BTCUSDT", placeResult.OrderId);

            // Assert
            Assert.NotNull(order);
            Assert.Equal(placeResult.OrderId, order.OrderId);
            Assert.Equal("BTCUSDT", order.Symbol);
        }

        [Fact]
        public async Task GetOrdersAsync_ShouldReturnOrders()
        {
            // Arrange
            var orderRequest = new PlaceOrderRequest
            {
                Symbol = "BTCUSDT",
                Side = OrderSide.Buy,
                OrderType = OrderType.Limit,
                PositionSide = PositionSide.Long,
                Quantity = 1,
                Price = 50000m,
                TimeInForce = TimeInForce.GTC
            };

            await _apiClient.PlaceOrderAsync(orderRequest);

            // Act
            var orders = await _apiClient.GetOrdersAsync("BTCUSDT");

            // Assert
            Assert.NotNull(orders);
            Assert.Single(orders);
            Assert.Equal("BTCUSDT", orders[0].Symbol);
        }

        [Fact]
        public async Task GetKlinesAsync_ShouldReturnKlines()
        {
            // Act
            var klines = await _apiClient.GetKlinesAsync("BTCUSDT", KlineInterval.OneHour, 10);

            // Assert
            Assert.NotNull(klines);
            Assert.True(klines.Count > 0);
            Assert.True(klines.Count <= 10);
        }

        [Fact]
        public async Task Get24hrPriceStatisticsAsync_ShouldReturnStatistics()
        {
            // Act
            var stats = await _apiClient.Get24hrPriceStatisticsAsync("BTCUSDT");

            // Assert
            Assert.NotNull(stats);
            Assert.Equal("BTCUSDT", stats.Symbol);
            Assert.True(stats.LastPrice > 0);
            Assert.True(stats.Volume > 0);
        }

        [Fact]
        public async Task GetLatestPriceAsync_ShouldReturnPrice()
        {
            // Act
            var price = await _apiClient.GetLatestPriceAsync("BTCUSDT");

            // Assert
            Assert.True(price > 0);
        }

        [Fact]
        public async Task ResetSimulatedAccountAsync_ShouldResetAccount()
        {
            // Arrange
            var initialBalance = 50000m;

            // Act
            await _apiClient.ResetSimulatedAccountAsync(initialBalance);
            var balances = await _apiClient.GetSimulatedBalanceAsync();

            // Assert
            Assert.Single(balances);
            Assert.Equal(initialBalance, balances[0].AvailableBalance);
        }

        [Fact]
        public async Task SetSimulatedPriceAsync_ShouldUpdatePrice()
        {
            // Arrange
            var symbol = "BTCUSDT";
            var newPrice = 60000m;

            // Act
            await _apiClient.SetSimulatedPriceAsync(symbol, newPrice);
            var price = await _apiClient.GetLatestPriceAsync(symbol);

            // Assert
            Assert.Equal(newPrice, price);
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