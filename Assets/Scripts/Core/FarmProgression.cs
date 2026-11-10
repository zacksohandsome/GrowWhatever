using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GrowWhatever.Core
{
    public sealed class ProcessingJob
    {
        public string RecipeId { get; }
        public double RemainingSeconds { get; internal set; }
        internal ProcessingJob(string recipe, double seconds) { RecipeId = recipe; RemainingSeconds = seconds; }
    }

    public sealed partial class FarmGame
    {
        public const int QueueCapacity = 4;
        private readonly List<ProcessingJob> jobs = new List<ProcessingJob>();
        private readonly HashSet<string> researched = new HashSet<string>();
        private readonly HashSet<string> upgraded = new HashSet<string>();
        private IGrowthEffect[] growthEffects;
        public FarmRules Rules { get; private set; }
        public ReadOnlyCollection<ProcessingJob> Jobs { get; private set; }
        public bool UsedNeighborBoost { get; private set; }
        public bool HarvestedMoonEssence { get; private set; }
        public event Action<int> ProcessingCompleted;
        public bool IsGoalComplete => IsUnlocked("moonflower") && upgraded.Count > 0 && UsedNeighborBoost && HarvestedMoonEssence;
        public double GrowthTimeMultiplier => Rules.Upgrades.Where(x => upgraded.Contains(x.Id)).Aggregate(1d, (v, x) => v * x.GrowthTimeMultiplier);
        public int YieldBonus => Rules.Upgrades.Where(x => upgraded.Contains(x.Id)).Sum(x => x.YieldBonus);

        private void InitializeProgression(FarmRules rules)
        {
            Rules = rules;
            Jobs = jobs.AsReadOnly();
            foreach (var recipe in rules.Recipes)
            {
                if (!produce.ContainsKey(recipe.InputId) || produce.ContainsKey(recipe.OutputId))
                    throw new ArgumentException("Recipe items must form the configured single-layer chain.");
                produce.Add(recipe.OutputId, 0);
            }
            growthEffects = content.Values.Where(x => x.GrowthEffect != null).Select(x => x.GrowthEffect).ToArray();
            foreach (var research in rules.Research)
                if (!content.ContainsKey(research.UnlockGrowableId)) throw new ArgumentException("Unknown research unlock.");
            foreach (var cost in rules.Research.Select(x => x.Cost).Concat(rules.Upgrades.Select(x => x.Cost)))
                if (cost.Items.Keys.Any(x => !produce.ContainsKey(x))) throw new ArgumentException("Unknown cost item.");
        }

        public bool IsResearched(string id) => researched.Contains(id);
        public bool IsUpgraded(string id) => upgraded.Contains(id);
        public bool IsUnlocked(string id) => content.TryGetValue(id, out var spec) &&
            (spec.InitiallyUnlocked || Rules.Research.Any(x => x.UnlockGrowableId == id && researched.Contains(x.Id)));
        public bool HasPrerequisite(string required) => string.IsNullOrEmpty(required) || researched.Contains(required);
        public bool CanPay(ResourceCost cost) => Money >= cost.Money && cost.Items.All(x => produce[x.Key] >= x.Value);

        public int ItemPrice(string id)
        {
            if (content.TryGetValue(id, out var spec)) return spec.ProducePrice;
            return Rules.Recipes.FirstOrDefault(x => x.OutputId == id)?.OutputPrice ?? 0;
        }

        public string ItemName(string id) => content.TryGetValue(id, out var spec) ? spec.ProduceName :
            Rules.Recipes.First(x => x.OutputId == id).OutputName;

        public bool CanQueue(string id)
        {
            var recipe = Rules.Recipes.FirstOrDefault(x => x.Id == id);
            return recipe != null && HasPrerequisite(recipe.RequiredResearch) && jobs.Count < QueueCapacity &&
                produce[recipe.InputId] >= recipe.InputAmount;
        }

        public bool QueueRecipe(string id)
        {
            if (!CanQueue(id)) return false;
            var recipe = Rules.Recipes.First(x => x.Id == id);
            produce[recipe.InputId] -= recipe.InputAmount;
            jobs.Add(new ProcessingJob(id, recipe.Seconds));
            Changed?.Invoke();
            return true;
        }

        private bool AdvanceProcessing(double elapsed)
        {
            bool changed = false;
            int completed = 0;
            while (jobs.Count > 0 && elapsed > 0)
            {
                var job = jobs[0];
                double used = Math.Min(elapsed, job.RemainingSeconds);
                job.RemainingSeconds -= used;
                elapsed -= used;
                changed |= used > 0;
                if (job.RemainingSeconds > 0) break;
                var recipe = Rules.Recipes.First(x => x.Id == job.RecipeId);
                // An inventory at its integer limit pauses delivery rather than losing materials.
                if (produce[recipe.OutputId] > int.MaxValue - recipe.OutputAmount) break;
                produce[recipe.OutputId] += recipe.OutputAmount;
                jobs.RemoveAt(0);
                completed++;
                changed = true;
            }
            if (completed > 0) ProcessingCompleted?.Invoke(completed);
            return changed;
        }

        public bool CanResearch(string id)
        {
            var spec = Rules.Research.FirstOrDefault(x => x.Id == id);
            return spec != null && !researched.Contains(id) && HasPrerequisite(spec.RequiredResearch) && CanPay(spec.Cost);
        }

        public bool Research(string id)
        {
            if (!CanResearch(id)) return false;
            Pay(Rules.Research.First(x => x.Id == id).Cost);
            researched.Add(id);
            Changed?.Invoke();
            return true;
        }

        public bool CanUpgrade(string id)
        {
            var spec = Rules.Upgrades.FirstOrDefault(x => x.Id == id);
            return spec != null && !upgraded.Contains(id) && HasPrerequisite(spec.RequiredResearch) && CanPay(spec.Cost);
        }

        public bool Upgrade(string id)
        {
            if (!CanUpgrade(id)) return false;
            Pay(Rules.Upgrades.First(x => x.Id == id).Cost);
            upgraded.Add(id);
            Changed?.Invoke();
            return true;
        }

        private void Pay(ResourceCost cost)
        {
            Money -= cost.Money;
            foreach (var item in cost.Items) produce[item.Key] -= item.Value;
        }

        public double NeighborMultiplier(FarmPlot plot) => growthEffects.Select(x => x.Multiplier(plot, Plots, Definition)).DefaultIfEmpty(1).Max();
        public double RemainingGrowthSeconds(FarmPlot plot) => plot.IsEmpty ? 0 :
            Math.Max(0, content[plot.GrowableId].GrowthSeconds - plot.Progress) * GrowthTimeMultiplier / NeighborMultiplier(plot);
    }
}
