using System;
using System.Collections.Generic;
using System.Linq;
using GrowWhatever.Core;
using NUnit.Framework;

namespace GrowWhatever.Tests
{
    public sealed class FarmProgressionTests
    {
        private static readonly long Now = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc).Ticks;
        public static GrowableSpec[] Definitions() => new[]
        {
            new GrowableSpec("carrot", "Carrot", 2, 30, 3, 1), new GrowableSpec("wheat", "Wheat", 4, 60, 3, 2),
            new GrowableSpec("iron", "Iron", 8, 90, 2, 6, false),
            new GrowableSpec("moonflower", "Moonflower", 15, 120, 1, 25, false, new MoonflowerGrowthEffect(1.25))
        };
        private static ResourceCost Cost(int money, params (string id, int amount)[] items) =>
            new ResourceCost(money, items.ToDictionary(x => x.id, x => x.amount));
        public static FarmRules Rules() => new FarmRules(new[]
        {
            new RecipeSpec("flour", "Flour", "wheat", 3, "flour", "Flour", 1, 9, 30),
            new RecipeSpec("parts", "Parts", "iron", 2, "parts", "Parts", 1, 16, 45, "iron")
        }, new[]
        {
            new ResearchSpec("iron", "Iron", null, "iron", Cost(20, ("carrot",6), ("flour",2))),
            new ResearchSpec("moonflower", "Moonflower", "iron", "moonflower", Cost(40, ("parts",4), ("flour",3)))
        }, new[]
        {
            new UpgradeSpec("growth", "Growth", "iron", Cost(40,("parts",2)), 0.9, 0),
            new UpgradeSpec("yield", "Yield", "iron", Cost(80,("parts",4)), 1, 1)
        });
        public static FarmGame Fresh() => new FarmGame(Definitions(), 20,
            new Dictionary<string,int> { {"carrot",5}, {"wheat",5} }, Rules());
        public static FarmGame Prepared(bool moonflower = true)
        {
            var state = Fresh().Capture(Now);
            state.Money = 500;
            state.Research.Add("iron");
            if (moonflower) state.Research.Add("moonflower");
            foreach (var item in state.Items) item.Amount = 20;
            foreach (var seed in state.Seeds) seed.Amount = seed.Id == "moonflower" && !moonflower ? 0 : 5;
            return FarmGame.Restore(Definitions(), Rules(), state);
        }

        [Test] public void LockedContentAndInsufficientResearchNeverDeductResources()
        {
            var game = Fresh();
            Assert.That(game.BuySeed("iron"), Is.False);
            Assert.That(game.QueueRecipe("parts"), Is.False);
            Assert.That(game.Research("iron"), Is.False);
            Assert.That(game.Money, Is.EqualTo(20));
            Assert.That(game.ProduceCount("carrot"), Is.Zero);
            var state = game.Capture(Now);
            state.Items.First(x=>x.Id=="carrot").Amount = 6;
            state.Items.First(x=>x.Id=="flour").Amount = 2;
            game = FarmGame.Restore(Definitions(),Rules(),state);
            Assert.That(game.Research("iron"), Is.True);
            Assert.That(game.IsUnlocked("iron"), Is.True);
            Assert.That(game.Money, Is.Zero);
            Assert.That(game.ProduceCount("flour"), Is.Zero);
            Assert.That(game.Research("iron"), Is.False);
            Assert.That(game.IsUnlocked("moonflower"), Is.False);
        }

        [Test] public void QueueIsFifoAndCapacityFailureDoesNotConsumeMaterials()
        {
            var game = Prepared();
            foreach (string id in new[]{"flour","parts","flour","parts"}) Assert.That(game.QueueRecipe(id),Is.True);
            int before = game.ProduceCount("wheat");
            Assert.That(game.QueueRecipe("flour"),Is.False);
            Assert.That(game.ProduceCount("wheat"),Is.EqualTo(before));
            game.Advance(31);
            Assert.That(game.ProduceCount("flour"),Is.EqualTo(21));
            Assert.That(game.Jobs[0].RecipeId,Is.EqualTo("parts"));
            Assert.That(game.Jobs[0].RemainingSeconds,Is.EqualTo(44));
            game.Advance(119);
            Assert.That(game.Jobs,Is.Empty);
            Assert.That(game.ProduceCount("parts"),Is.EqualTo(22));
            Assert.That(game.ProduceCount("flour"),Is.EqualTo(22));
            game.Advance(10000);
            Assert.That(game.ProduceCount("parts"),Is.EqualTo(22));
        }

        [Test] public void MoonflowerOnlyBoostsOrthogonalNonSourcesAndDoesNotStack()
        {
            var game=Prepared();
            game.Plant(0,"moonflower"); game.Plant(2,"moonflower"); game.Plant(1,"carrot"); game.Plant(4,"wheat");
            Assert.That(game.UsedNeighborBoost,Is.False);
            game.Advance(10);
            Assert.That(game.Plots[1].Progress,Is.EqualTo(12.5));
            Assert.That(game.Plots[4].Progress,Is.EqualTo(10));
            Assert.That(game.Plots[0].Progress,Is.EqualTo(10));
            Assert.That(game.UsedNeighborBoost,Is.True);
        }

        [Test] public void MatureAuraRemovalAndGrowthUpgradeDoNotRewritePriorProgress()
        {
            var game=Prepared(); game.Plant(0,"moonflower"); game.Advance(120); game.Plant(1,"wheat");
            game.Advance(8);
            Assert.That(game.Plots[1].Progress,Is.EqualTo(10));
            game.Harvest(0); game.Advance(10);
            Assert.That(game.Plots[1].Progress,Is.EqualTo(20));
            Assert.That(game.Upgrade("growth"),Is.True);
            Assert.That(game.Plots[1].Progress,Is.EqualTo(20));
            game.Advance(9);
            Assert.That(game.Plots[1].Progress,Is.EqualTo(30).Within(1e-8));
            Assert.That(game.Upgrade("growth"),Is.False);
        }

        [Test] public void YieldUpgradeAppliesAtHarvestIncludingAlreadyMaturePlants()
        {
            var game=Prepared(); game.Plant(0,"carrot"); game.Plant(1,"carrot"); game.Advance(30);
            game.Harvest(0);
            Assert.That(game.ProduceCount("carrot"),Is.EqualTo(23));
            game.Upgrade("yield");
            Assert.That(game.ProduceCount("carrot"),Is.EqualTo(23));
            game.Harvest(1);
            Assert.That(game.ProduceCount("carrot"),Is.EqualTo(27));
        }

        [Test] public void GoalRequiresActualBoostUpgradeAndManualMoonflowerHarvestAndNeverRegresses()
        {
            var game=Prepared(); game.Upgrade("growth"); game.Plant(0,"moonflower"); game.Plant(1,"carrot");
            Assert.That(game.IsGoalComplete,Is.False);
            game.Advance(120); game.HarvestAll();
            Assert.That(game.UsedNeighborBoost,Is.True);
            Assert.That(game.HarvestedMoonEssence,Is.False);
            Assert.That(game.IsGoalComplete,Is.False);
            game.Plant(0,"moonflower"); game.Advance(120); game.Harvest(0);
            Assert.That(game.IsGoalComplete,Is.True);
            while(game.SellProduce("moonflower")) { }
            Assert.That(game.IsGoalComplete,Is.True);
        }

        [Test] public void SnapshotRoundTripAndOfflineAdvanceMatchOnlineSegments()
        {
            var online=Prepared(); online.Upgrade("growth"); online.Plant(0,"moonflower"); online.Plant(1,"iron");
            online.SetBulkHarvest(0,false); online.QueueRecipe("flour"); online.QueueRecipe("parts"); online.Advance(5);
            var saved=online.Capture(Now); var offline=FarmGame.Restore(Definitions(),Rules(),saved);
            for(int i=0;i<200;i++) online.Advance(1);
            offline.Advance(200);
            Assert.That(offline.Plots.Select(x=>x.Progress),Is.EqualTo(online.Plots.Select(x=>x.Progress)).Within(1e-7));
            Assert.That(offline.ProduceCount("parts"),Is.EqualTo(online.ProduceCount("parts")));
            Assert.That(offline.Jobs.Count,Is.EqualTo(online.Jobs.Count));
            Assert.That(offline.Plots[0].IncludeInBulkHarvest,Is.False);
            Assert.That(offline.Money,Is.EqualTo(online.Money));
            var reloaded=FarmGame.Restore(Definitions(),Rules(),offline.Capture(Now)); reloaded.Advance(-500);
            Assert.That(reloaded.ProduceCount("parts"),Is.EqualTo(offline.ProduceCount("parts")));
        }

        [Test] public void InvalidSnapshotsAreRejectedAsAWhole()
        {
            var state=Fresh().Capture(Now); state.Plots[1].Id=state.Plots[0].Id;
            Assert.Throws<ArgumentException>(()=>FarmGame.Restore(Definitions(),Rules(),state));
            state=Fresh().Capture(Now); state.Items[0].Amount=-1;
            Assert.Throws<ArgumentException>(()=>FarmGame.Restore(Definitions(),Rules(),state));
            state=Fresh().Capture(Now); state.Plots[0].Progress=double.NaN;
            Assert.Throws<ArgumentException>(()=>FarmGame.Restore(Definitions(),Rules(),state));
            state=Fresh().Capture(Now); state.Version=999;
            Assert.Throws<ArgumentException>(()=>FarmGame.Restore(Definitions(),Rules(),state));
            state=Fresh().Capture(Now); state.Upgrades.Add("growth");
            Assert.Throws<ArgumentException>(()=>FarmGame.Restore(Definitions(),Rules(),state));
        }

        [Test] public void FourQueuedJobsPreventEmergencySupplyEvenWithoutOtherResources()
        {
            var saved=Fresh().Capture(Now); saved.Money=0;
            foreach(var seed in saved.Seeds) seed.Amount=0;
            saved.Items.First(x=>x.Id=="wheat").Amount=3;
            var game=FarmGame.Restore(Definitions(),Rules(),saved); game.QueueRecipe("flour");
            Assert.That(game.CanClaimSupply,Is.False);
            game.Advance(30); Assert.That(game.CanClaimSupply,Is.False);
            game.SellProduce("flour"); Assert.That(game.CanClaimSupply,Is.False);
        }
    }
}
