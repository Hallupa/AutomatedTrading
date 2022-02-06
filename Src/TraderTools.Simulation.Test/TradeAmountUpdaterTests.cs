using System.Collections.Generic;
using FluentAssertions;
using Hallupa.TraderTools.Basics;
using Hallupa.TraderTools.Simulation;
using NUnit.Framework;
using TraderTools.Basics;

namespace TraderTools.Simulation.Test
{
    public class TradeAmountUpdaterTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void UpdateTradeAndBalance_WhenLongEntryQuantityMoreThanBalance_UpdatesTradeAndBalance()
        {
            // Arrange
            var sut = new TradeAmountUpdater();
            var commission = 0.01M;
            var ethPrice = 1000M;
            var usdtBalance = 10000M;
            Dictionary<string, AssetBalance> balances = new Dictionary<string, AssetBalance>
            {
                { "USDT", new AssetBalance("USDT", usdtBalance) }
            };

            var trade = new Trade
            {
                EntryQuantity = 100,
                EntryPrice = ethPrice,
                BaseAsset = "ETH",
                Market = "ETHUSDT",
                TradeDirection = TradeDirection.Long
            };

            var maxBuyCostExcludingFee = usdtBalance / (1M + commission);
            var maxEthQuantity = maxBuyCostExcludingFee / ethPrice;

            // Act
            sut.UpdateTradeAndBalance(trade, commission, BrokerKind.Trade, balances);

            // Assert
            trade.Should().BeEquivalentTo(
                new Trade
                {
                    EntryQuantity = maxEthQuantity,
                    EntryPrice = ethPrice,
                    BaseAsset = "ETH",
                    Market = "ETHUSDT",
                    TradeDirection = TradeDirection.Long,
                    RiskAmount = 0M
                });

            balances["USDT"].Should().BeEquivalentTo(new AssetBalance("USDT", 0M));
            balances["ETH"].Should().BeEquivalentTo(new AssetBalance("ETH", maxEthQuantity));
        }

        [Test]
        public void UpdateTradeAndBalance_WhenLongEntryQuantityLessThanBalance_UpdatesTradeAndBalance()
        {
            // Arrange
            var sut = new TradeAmountUpdater();
            var commission = 0.01M;
            var ethPrice = 1000M;
            var quantity = 3;
            var usdtBalance = 10000M;
            Dictionary<string, AssetBalance> balances = new Dictionary<string, AssetBalance>
            {
                { "USDT", new AssetBalance("USDT", usdtBalance) }
            };

            var trade = new Trade
            {
                EntryQuantity = quantity,
                EntryPrice = ethPrice,
                BaseAsset = "ETH",
                Market = "ETHUSDT",
                TradeDirection = TradeDirection.Long
            };

            var buyCostExcludingFee = quantity * ethPrice;
            var fee = buyCostExcludingFee * commission;

            // Act
            sut.UpdateTradeAndBalance(trade, commission, BrokerKind.Trade, balances);

            // Assert
            trade.Should().BeEquivalentTo(
                new Trade
                {
                    EntryQuantity = quantity,
                    EntryPrice = ethPrice,
                    BaseAsset = "ETH",
                    Market = "ETHUSDT",
                    TradeDirection = TradeDirection.Long,
                    RiskAmount = 0M
                });

            balances["USDT"].Should().BeEquivalentTo(new AssetBalance("USDT", usdtBalance - buyCostExcludingFee - fee));
            balances["ETH"].Should().BeEquivalentTo(new AssetBalance("ETH", quantity));
        }
        
        [Test]
        public void UpdateTradeAndBalance_WhenShortEntryQuantityLessThanBalance_UpdatesTradeAndBalance()
        {
            // Arrange
            var sut = new TradeAmountUpdater();
            var commission = 0.01M;
            var ethPrice = 1000M;
            var quantity = 3;
            var ethBalance = 10M;
            Dictionary<string, AssetBalance> balances = new Dictionary<string, AssetBalance>
            {
                { "ETH", new AssetBalance("ETH", ethBalance) }
            };

            var trade = new Trade
            {
                EntryQuantity = quantity,
                EntryPrice = ethPrice,
                BaseAsset = "ETH",
                Market = "ETHUSDT",
                TradeDirection = TradeDirection.Short
            };

            // Act
            sut.UpdateTradeAndBalance(trade, commission, BrokerKind.Trade, balances);

            // Assert
            trade.Should().BeEquivalentTo(
                new Trade
                {
                    EntryQuantity = quantity,
                    EntryPrice = ethPrice,
                    BaseAsset = "ETH",
                    Market = "ETHUSDT",
                    TradeDirection = TradeDirection.Short,
                    RiskAmount = 0M
                });

            balances["ETH"].Should().BeEquivalentTo(new AssetBalance("ETH", ethBalance - quantity));
            balances["USDT"].Should().BeEquivalentTo(new AssetBalance("USDT", (quantity * ethPrice) - (quantity * commission * ethPrice)));
        }

        [Test]
        public void UpdateTradeAndBalance_WhenShortEntryQuantityMoreThanBalance_UpdatesTradeAndBalance()
        {
            // Arrange
            var sut = new TradeAmountUpdater();
            var commission = 0.01M;
            var ethPrice = 1000M;
            var ethBalance = 10M;
            Dictionary<string, AssetBalance> balances = new Dictionary<string, AssetBalance>
            {
                { "ETH", new AssetBalance("ETH", ethBalance) }
            };

            var trade = new Trade
            {
                EntryQuantity = 15,
                EntryPrice = ethPrice,
                BaseAsset = "ETH",
                Market = "ETHUSDT",
                TradeDirection = TradeDirection.Short
            };

            // Act
            sut.UpdateTradeAndBalance(trade, commission, BrokerKind.Trade, balances);

            // Assert
            trade.Should().BeEquivalentTo(
                new Trade
                {
                    EntryQuantity = 10M,
                    EntryPrice = ethPrice,
                    BaseAsset = "ETH",
                    Market = "ETHUSDT",
                    TradeDirection = TradeDirection.Short,
                    RiskAmount = 0M
                });

            balances["ETH"].Should().BeEquivalentTo(new AssetBalance("ETH", 0M));
            balances["USDT"].Should().BeEquivalentTo(new AssetBalance("USDT", (10M * ethPrice) - (10M * ethPrice * commission)));
        }
    }
}