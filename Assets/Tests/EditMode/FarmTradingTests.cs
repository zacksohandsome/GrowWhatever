using System;
using System.Collections.Generic;
using System.Linq;
using GrowWhatever.Core;
using NUnit.Framework;

namespace GrowWhatever.Tests
{
    public sealed class FarmTradingTests
    {
        private static FarmGame Fresh(int money = 46, int seeds = 8) => new FarmGame(
            FarmProgressionTests.Definitions(), money, new Dictionary<string,int> { { "carrot", seeds } }, FarmProgressionTests.Rules());

        private static FarmGame WithItems(int money, string id, int amount)
        {
            var snapshot = Fresh(money).Capture(DateTime.UtcNow.Ticks);
            snapshot.Items.First(x => x.Id == id).Amount = amount;
            return FarmGame.Restore(FarmProgressionTests.Definitions(), FarmProgressionTests.Rules(), snapshot);
        }

        [Test] public void BuyingUsesAffordableQuantityAndRemainingInventorySpace()
        {
            var game = Fresh();
            Assert.That(game.QuoteTrade("carrot", FarmTradeKind.BuySeed).Maximum, Is.EqualTo(23));
            Assert.That(game.Trade("carrot", FarmTradeKind.BuySeed, 23), Is.True);
            Assert.That(game.Money, Is.Zero);
            Assert.That(game.SeedCount("carrot"), Is.EqualTo(31));
            game = Fresh(int.MaxValue, FarmGame.InventoryLimit - 8);
            Assert.That(game.QuoteTrade("carrot", FarmTradeKind.BuySeed).Maximum, Is.EqualTo(8));
            Assert.That(game.Trade("carrot", FarmTradeKind.BuySeed, 8), Is.True);
            Assert.That(game.SeedCount("carrot"), Is.EqualTo(FarmGame.InventoryLimit));
            Assert.That(game.BuySeed("carrot"), Is.False);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(24)] [TestCase(int.MaxValue)]
        public void InvalidPurchasesNeverPartiallySpendOrNotify(int quantity)
        {
            var game = Fresh(); int changes = 0; game.Changed += () => changes++;
            Assert.That(game.Trade("carrot", FarmTradeKind.BuySeed, quantity), Is.False);
            Assert.That(game.Money, Is.EqualTo(46));
            Assert.That(game.SeedCount("carrot"), Is.EqualTo(8));
            Assert.That(changes, Is.Zero);
        }

        [Test] public void LockedOrUnknownTradesAreRejected()
        {
            var game = Fresh();
            Assert.That(game.Trade("iron", FarmTradeKind.BuySeed, 1), Is.False);
            Assert.That(game.QuoteTrade("iron", FarmTradeKind.BuySeed).Maximum, Is.Zero);
            Assert.That(game.Trade("missing", FarmTradeKind.SellProduce, 1), Is.False);
            Assert.That(game.Trade("carrot", (FarmTradeKind)99, 1), Is.False);
        }

        [Test] public void SeedSalesUseOriginalPriceAndNotifyOncePerWholeTrade()
        {
            var game = Fresh(); int changes = 0; game.Changed += () => changes++;
            Assert.That(game.Trade("carrot", FarmTradeKind.SellSeed, 8), Is.True);
            Assert.That(game.Money, Is.EqualTo(62));
            Assert.That(game.SeedCount("carrot"), Is.Zero);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(game.Trade("carrot", FarmTradeKind.SellSeed, 8), Is.False);
            Assert.That(game.Money, Is.EqualTo(62));
        }

        [TestCase("carrot", 1)] [TestCase("flour", 9)]
        public void HarvestAndProcessedGoodsShareAtomicSales(string id, int price)
        {
            var game = WithItems(0, id, 23);
            Assert.That(game.QuoteTrade(id, FarmTradeKind.SellProduce).Maximum, Is.EqualTo(23));
            Assert.That(game.Trade(id, FarmTradeKind.SellProduce, 24), Is.False);
            Assert.That(game.ProduceCount(id), Is.EqualTo(23));
            Assert.That(game.Trade(id, FarmTradeKind.SellProduce, 23), Is.True);
            Assert.That(game.ProduceCount(id), Is.Zero);
            Assert.That(game.Money, Is.EqualTo(23 * price));
        }

        [Test] public void StalePreviewDoesNotAuthorizePartialSale()
        {
            var game = WithItems(0, "carrot", 20);
            var quote = game.QuoteTrade("carrot", FarmTradeKind.SellProduce);
            game.Trade("carrot", FarmTradeKind.SellProduce, 5);
            Assert.That(game.Trade("carrot", FarmTradeKind.SellProduce, quote.Maximum), Is.False);
            Assert.That(game.ProduceCount("carrot"), Is.EqualTo(15));
            Assert.That(game.Money, Is.EqualTo(5));
        }

        [Test] public void LargeSaleCannotOverflowMoneyOrLoseInventory()
        {
            var game = WithItems(0, "flour", FarmGame.InventoryLimit);
            int maximum = int.MaxValue / 9;
            Assert.That(game.QuoteTrade("flour", FarmTradeKind.SellProduce).Maximum, Is.EqualTo(maximum));
            Assert.That(game.Trade("flour", FarmTradeKind.SellProduce, FarmGame.InventoryLimit), Is.False);
            Assert.That(game.Money, Is.Zero);
            Assert.That(game.Trade("flour", FarmTradeKind.SellProduce, maximum), Is.True);
            Assert.That(game.Money, Is.EqualTo((long)maximum * 9));
            Assert.That(game.SellProduce("flour"), Is.False);
            Assert.That(game.ProduceCount("flour"), Is.EqualTo(FarmGame.InventoryLimit - maximum));
        }

        [Test] public void HarvestAtCapacityKeepsPlantUntilThereIsRoom()
        {
            var game = WithItems(0, "carrot", FarmGame.InventoryLimit - 2);
            game.Plant(0, "carrot"); game.Advance(30);
            Assert.That(game.Harvest(0), Is.False);
            Assert.That(game.Plots[0].IsEmpty, Is.False);
            game.SellProduce("carrot");
            Assert.That(game.Harvest(0), Is.True);
            Assert.That(game.ProduceCount("carrot"), Is.EqualTo(FarmGame.InventoryLimit));
        }

        [Test] public void ProcessingAtCapacityWaitsWithoutLosingOutput()
        {
            var game = WithItems(0, "flour", FarmGame.InventoryLimit);
            var saved = game.Capture(DateTime.UtcNow.Ticks);
            saved.Items.First(x => x.Id == "wheat").Amount = 3;
            game = FarmGame.Restore(FarmProgressionTests.Definitions(), FarmProgressionTests.Rules(), saved);
            Assert.That(game.QueueRecipe("flour"), Is.True);
            game.Advance(30);
            Assert.That(game.Jobs.Count, Is.EqualTo(1));
            Assert.That(game.ProduceCount("flour"), Is.EqualTo(FarmGame.InventoryLimit));
            game.SellProduce("flour"); game.Advance(0.1);
            Assert.That(game.Jobs, Is.Empty);
            Assert.That(game.ProduceCount("flour"), Is.EqualTo(FarmGame.InventoryLimit));
        }

        [TestCase("999999999999999999999999999999999", 23)]
        [TestCase("-10", 0)] [TestCase("", 0)] [TestCase("3", 3)] [TestCase("00023", 23)]
        [TestCase("12x", 0)]
        public void NumericInputSaturatesSafely(string input, int expected)
        { Assert.That(TradeQuantityView.ParseQuantity(input, 23), Is.EqualTo(expected)); }
    }
}
