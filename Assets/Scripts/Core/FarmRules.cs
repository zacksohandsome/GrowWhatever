using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GrowWhatever.Core
{
    public sealed class ResourceCost
    {
        public int Money { get; }
        public IReadOnlyDictionary<string, int> Items { get; }
        public ResourceCost(int money, IReadOnlyDictionary<string, int> items)
        {
            if (money < 0 || items.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value <= 0))
                throw new ArgumentException("Invalid resource cost.");
            Money = money;
            Items = new ReadOnlyDictionary<string, int>(items.ToDictionary(x => x.Key, x => x.Value));
        }
    }

    public sealed class RecipeSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string InputId { get; }
        public int InputAmount { get; }
        public string OutputId { get; }
        public string OutputName { get; }
        public int OutputAmount { get; }
        public int OutputPrice { get; }
        public double Seconds { get; }
        public string RequiredResearch { get; }
        public RecipeSpec(string id, string name, string input, int amount, string output, string outputName,
            int outputAmount, int price, double seconds, string requiredResearch = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) ||
                amount <= 0 || outputAmount <= 0 || price <= 0 || seconds <= 0 || double.IsInfinity(seconds) || double.IsNaN(seconds))
                throw new ArgumentException("Invalid recipe settings.");
            Id = id; DisplayName = name; InputId = input; InputAmount = amount; OutputId = output;
            OutputName = outputName; OutputAmount = outputAmount; OutputPrice = price; Seconds = seconds;
            RequiredResearch = requiredResearch;
        }
    }

    public sealed class ResearchSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string RequiredResearch { get; }
        public string UnlockGrowableId { get; }
        public ResourceCost Cost { get; }
        public ResearchSpec(string id, string name, string required, string unlock, ResourceCost cost)
        { Id = id; DisplayName = name; RequiredResearch = required; UnlockGrowableId = unlock; Cost = cost; }
    }

    public sealed class UpgradeSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string RequiredResearch { get; }
        public ResourceCost Cost { get; }
        public double GrowthTimeMultiplier { get; }
        public int YieldBonus { get; }
        public UpgradeSpec(string id, string name, string required, ResourceCost cost, double growthTimeMultiplier, int yieldBonus)
        {
            if (growthTimeMultiplier <= 0 || double.IsInfinity(growthTimeMultiplier) || double.IsNaN(growthTimeMultiplier) || yieldBonus < 0)
                throw new ArgumentException("Invalid upgrade settings.");
            Id = id; DisplayName = name; RequiredResearch = required; Cost = cost;
            GrowthTimeMultiplier = growthTimeMultiplier; YieldBonus = yieldBonus;
        }
    }

    public sealed class FarmRules
    {
        public IReadOnlyList<RecipeSpec> Recipes { get; }
        public IReadOnlyList<ResearchSpec> Research { get; }
        public IReadOnlyList<UpgradeSpec> Upgrades { get; }
        public FarmRules(IEnumerable<RecipeSpec> recipes, IEnumerable<ResearchSpec> research, IEnumerable<UpgradeSpec> upgrades)
        {
            Recipes = Array.AsReadOnly(recipes.ToArray());
            Research = Array.AsReadOnly(research.ToArray());
            Upgrades = Array.AsReadOnly(upgrades.ToArray());
            if (Recipes.Select(x => x.Id).Distinct().Count() != Recipes.Count ||
                Research.Select(x => x.Id).Distinct().Count() != Research.Count ||
                Upgrades.Select(x => x.Id).Distinct().Count() != Upgrades.Count)
                throw new ArgumentException("Content IDs must be unique.");
        }
        public static FarmRules Empty => new FarmRules(Array.Empty<RecipeSpec>(), Array.Empty<ResearchSpec>(), Array.Empty<UpgradeSpec>());
    }
}
