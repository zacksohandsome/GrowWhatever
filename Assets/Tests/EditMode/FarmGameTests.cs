using System.Collections.Generic;
using System.Linq;
using GrowWhatever.Core;
using NUnit.Framework;

namespace GrowWhatever.Tests
{
    public sealed class FarmGameTests
    {
        private static FarmGame NewGame(int money = 20, int carrots = 5, int wheat = 5) => new FarmGame(
            new[] { new GrowableSpec("carrot", "Carrot", 2, 30, 3, 1), new GrowableSpec("wheat", "Wheat", 4, 60, 3, 2) },
            money, new Dictionary<string, int> { { "carrot", carrots }, { "wheat", wheat } });

        [Test]
        public void BulkPlantUsesOnlyAvailableSeedsAndFillsEmptyPlotsInStableOrder()
        {
            var game = NewGame(carrots: 3);
            foreach (int i in new[] { 0, 2, 5, 8 }) Assert.That(game.Plant(i, "wheat"), Is.True);
            Assert.That(game.PlantableCount("carrot"), Is.EqualTo(3));
            Assert.That(game.PlantAll("carrot"), Is.EqualTo(3));
            Assert.That(game.SeedCount("carrot"), Is.Zero);
            Assert.That(game.Plots.Where(x => x.GrowableId == "carrot").Select(x => x.Id),
                Is.EqualTo(new[] { "plot-0-1", "plot-1-0", "plot-1-1" }));
            Assert.That(game.PlantAll("carrot"), Is.Zero);
            Assert.That(game.Plant(0, "carrot"), Is.False);
            Assert.That(game.Plots[0].GrowableId, Is.EqualTo("wheat"));
        }

        [Test]
        public void ImmatureCannotBeHarvestedAndMatureHarvestCannotPayTwice()
        {
            var game = NewGame();
            game.Plant(0, "carrot");
            Assert.That(game.Harvest(0), Is.False);
            game.Advance(29.9);
            Assert.That(game.Harvest(0), Is.False);
            game.Advance(0.2);
            Assert.That(game.Plots[0].Progress, Is.EqualTo(30));
            Assert.That(game.Harvest(0), Is.True);
            Assert.That(game.Harvest(0), Is.False);
            Assert.That(game.ProduceCount("carrot"), Is.EqualTo(3));
            Assert.That(game.Plots[0].IsEmpty, Is.True);
        }

        [Test]
        public void BulkExclusionPersistsAcrossManualHarvestAndReplant()
        {
            var game = NewGame();
            game.Plant(0, "carrot");
            game.Plant(1, "carrot");
            game.SetBulkHarvest(0, false);
            game.Advance(30);
            Assert.That(game.HarvestAll(), Is.EqualTo(1));
            Assert.That(game.Harvest(0), Is.True);
            game.Plant(0, "wheat");
            game.Advance(60);
            Assert.That(game.HarvestAll(), Is.Zero);
            Assert.That(game.Plots[0].IncludeInBulkHarvest, Is.False);
            Assert.That(game.Harvest(0), Is.True);
        }

        [Test]
        public void SpendingIsAtomicAndSeedsSellBackAtOriginalPrice()
        {
            var game = NewGame(money: 1, carrots: 0, wheat: 0);
            Assert.That(game.BuySeed("carrot"), Is.False);
            Assert.That(game.SellSeed("wheat"), Is.False);
            Assert.That(game.SellProduce("carrot"), Is.False);
            Assert.That(game.Money, Is.EqualTo(1));
            game = NewGame(money: 4, carrots: 0, wheat: 0);
            Assert.That(game.BuySeed("wheat"), Is.True);
            Assert.That(game.Money, Is.Zero);
            Assert.That(game.BuySeed("carrot"), Is.False);
            Assert.That(game.SellSeed("wheat"), Is.True);
            Assert.That(game.Money, Is.EqualTo(4));
            Assert.That(game.SellSeed("wheat"), Is.False);
        }

        [Test]
        public void SupplyRequiresNoUsableResourcesAndCannotBeClaimedTwice()
        {
            var game = NewGame(money: 1, carrots: 0, wheat: 0);
            Assert.That(game.ClaimSupply(), Is.True);
            Assert.That(game.ClaimSupply(), Is.False);
            game.Plant(0, "carrot");
            Assert.That(game.CanClaimSupply, Is.False);
            game.Advance(30);
            game.Harvest(0);
            Assert.That(game.CanClaimSupply, Is.False);
            Assert.That(NewGame(money: 2, carrots: 0, wheat: 0).CanClaimSupply, Is.False);
            Assert.That(NewGame(money: 0, carrots: 0, wheat: 1).CanClaimSupply, Is.False);
        }

        [Test]
        public void OnlineSegmentsMatchSingleAdvanceAndMaturityCapsWithoutAutoHarvest()
        {
            var online = NewGame();
            var offline = NewGame();
            online.Plant(0, "wheat");
            offline.Plant(0, "wheat");
            foreach (double seconds in new[] { 10d, 15d, 20d }) online.Advance(seconds);
            offline.Advance(45);
            Assert.That(online.Plots[0].Progress, Is.EqualTo(offline.Plots[0].Progress));
            online.Advance(-100);
            online.Advance(double.NaN);
            Assert.That(online.Plots[0].Progress, Is.EqualTo(45));
            online.Advance(1000000);
            Assert.That(online.Plots[0].Progress, Is.EqualTo(60));
            Assert.That(online.Plots[0].IsEmpty, Is.False);
            Assert.That(online.ProduceCount("wheat"), Is.Zero);
            Assert.That(online.SeedCount("wheat"), Is.EqualTo(4));
        }

        [Test]
        public void FullLoopEarnsMoneyAndReleasesPlotsForNextPlanting()
        {
            var game = NewGame(money: 2, carrots: 0, wheat: 0);
            game.BuySeed("carrot");
            game.Plant(0, "carrot");
            game.Advance(30);
            game.Harvest(0);
            for (int i = 0; i < 3; i++) Assert.That(game.SellProduce("carrot"), Is.True);
            Assert.That(game.Money, Is.EqualTo(3));
            Assert.That(game.BuySeed("carrot"), Is.True);
            Assert.That(game.Plant(0, "carrot"), Is.True);
        }
    }
}
