using System;
using System.Collections.Generic;
using System.Linq;

namespace GrowWhatever.Core
{
    [Serializable] public sealed class InventorySnapshot { public string Id; public int Amount; }
    [Serializable] public sealed class PlotSnapshot
    { public string Id; public string GrowableId; public double Progress; public bool IncludeInBulkHarvest; }
    [Serializable] public sealed class JobSnapshot { public string RecipeId; public double RemainingSeconds; }
    [Serializable] public sealed class FarmSnapshot
    {
        public int Version;
        public long SavedUtcTicks;
        public int Money;
        public List<InventorySnapshot> Seeds;
        public List<InventorySnapshot> Items;
        public List<PlotSnapshot> Plots;
        public List<JobSnapshot> Jobs;
        public List<string> Research;
        public List<string> Upgrades;
        public bool UsedNeighborBoost;
        public bool HarvestedMoonEssence;
    }

    public sealed partial class FarmGame
    {
        public const int SaveVersion = 1;
        public FarmSnapshot Capture(long savedUtcTicks) => new FarmSnapshot
        {
            Version = SaveVersion, SavedUtcTicks = savedUtcTicks, Money = Money,
            Seeds = seeds.Select(x => new InventorySnapshot { Id = x.Key, Amount = x.Value }).ToList(),
            Items = produce.Select(x => new InventorySnapshot { Id = x.Key, Amount = x.Value }).ToList(),
            Plots = plots.Select(x => new PlotSnapshot { Id = x.Id, GrowableId = x.GrowableId, Progress = x.Progress,
                IncludeInBulkHarvest = x.IncludeInBulkHarvest }).ToList(),
            Jobs = jobs.Select(x => new JobSnapshot { RecipeId = x.RecipeId, RemainingSeconds = x.RemainingSeconds }).ToList(),
            Research = researched.OrderBy(x => x).ToList(), Upgrades = upgraded.OrderBy(x => x).ToList(),
            UsedNeighborBoost = UsedNeighborBoost, HarvestedMoonEssence = HarvestedMoonEssence
        };

        /// <summary>Reject the entire snapshot before returning any restored state.</summary>
        public static FarmGame Restore(IEnumerable<GrowableSpec> definitions, FarmRules rules, FarmSnapshot saved)
        {
            if (saved == null || saved.Version != SaveVersion || saved.SavedUtcTicks <= 0 || saved.SavedUtcTicks > DateTime.MaxValue.Ticks || saved.Money < 0)
                throw new ArgumentException("Unsupported or invalid save header.");
            var game = new FarmGame(definitions, saved.Money, new Dictionary<string, int>(), rules);
            CopyInventory(saved.Seeds, game.seeds);
            CopyInventory(saved.Items, game.produce);
            if (saved.Research == null || saved.Upgrades == null || saved.Research.Distinct().Count() != saved.Research.Count ||
                saved.Upgrades.Distinct().Count() != saved.Upgrades.Count ||
                saved.Research.Any(x => !rules.Research.Any(r => r.Id == x)) || saved.Upgrades.Any(x => !rules.Upgrades.Any(u => u.Id == x)))
                throw new ArgumentException("Invalid progression data.");
            foreach (string id in saved.Research) game.researched.Add(id);
            foreach (string id in saved.Upgrades) game.upgraded.Add(id);
            if (rules.Research.Any(x => game.researched.Contains(x.Id) && !game.HasPrerequisite(x.RequiredResearch)) ||
                rules.Upgrades.Any(x => game.upgraded.Contains(x.Id) && !game.HasPrerequisite(x.RequiredResearch)))
                throw new ArgumentException("Invalid progression prerequisites.");
            if (saved.Plots == null || saved.Plots.Count != 9 || saved.Plots.Any(x => x == null) || saved.Plots.Select(x => x.Id).Distinct().Count() != 9)
                throw new ArgumentException("Invalid plot list.");
            foreach (var source in saved.Plots)
            {
                var target = game.plots.FirstOrDefault(x => x.Id == source.Id);
                if (target == null || !FiniteNonnegative(source.Progress) ||
                    (!string.IsNullOrEmpty(source.GrowableId) && (!game.IsUnlocked(source.GrowableId) || source.Progress > game.content[source.GrowableId].GrowthSeconds)) ||
                    (string.IsNullOrEmpty(source.GrowableId) && source.Progress != 0))
                    throw new ArgumentException("Invalid plot state.");
                target.GrowableId = string.IsNullOrEmpty(source.GrowableId) ? null : source.GrowableId;
                target.Progress = source.Progress;
                target.IncludeInBulkHarvest = source.IncludeInBulkHarvest;
            }
            if (saved.Jobs == null || saved.Jobs.Count > QueueCapacity) throw new ArgumentException("Invalid processing queue.");
            for (int i = 0; i < saved.Jobs.Count; i++)
            {
                var job = saved.Jobs[i];
                var recipe = job == null ? null : rules.Recipes.FirstOrDefault(x => x.Id == job.RecipeId);
                if (recipe == null || !game.HasPrerequisite(recipe.RequiredResearch) || !FiniteNonnegative(job.RemainingSeconds) ||
                    job.RemainingSeconds > recipe.Seconds || (i > 0 && job.RemainingSeconds != recipe.Seconds))
                    throw new ArgumentException("Invalid processing job.");
                game.jobs.Add(new ProcessingJob(job.RecipeId, job.RemainingSeconds));
            }
            if (game.seeds.Any(x => x.Value > 0 && !game.IsUnlocked(x.Key))) throw new ArgumentException("Locked seed inventory.");
            game.UsedNeighborBoost = saved.UsedNeighborBoost;
            game.HarvestedMoonEssence = saved.HarvestedMoonEssence;
            if ((saved.UsedNeighborBoost || saved.HarvestedMoonEssence) && !game.IsUnlocked("moonflower"))
                throw new ArgumentException("Invalid milestone flags.");
            return game;
        }

        private static bool FiniteNonnegative(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        private static void CopyInventory(List<InventorySnapshot> source, Dictionary<string, int> destination)
        {
            if (source == null || source.Count != destination.Count || source.Any(x => x == null || x.Id == null || x.Amount < 0) ||
                source.Select(x => x.Id).Distinct().Count() != source.Count || source.Any(x => !destination.ContainsKey(x.Id)))
                throw new ArgumentException("Invalid inventory.");
            foreach (var item in source) destination[item.Id] = item.Amount;
        }
    }
}
