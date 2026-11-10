using System;
using System.Collections.Generic;
using System.Linq;
using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    [Serializable] public sealed class ItemAmount
    { public string id; public int amount; public ItemAmount(string id, int amount) { this.id = id; this.amount = amount; } }

    [Serializable] public sealed class RecipeSettings
    {
        public string id, displayName, inputId, outputId, outputName, requiredResearch;
        public int inputAmount = 3, outputAmount = 1, outputPrice = 9;
        public float seconds = 30;
        public RecipeSpec ToSpec() => new RecipeSpec(id, displayName, inputId, inputAmount, outputId, outputName,
            outputAmount, outputPrice, seconds, requiredResearch);
    }

    [Serializable] public sealed class ResearchSettings
    {
        public string id, displayName, requiredResearch, unlockGrowableId;
        public int money;
        public ItemAmount[] items;
        public ResearchSpec ToSpec() => new ResearchSpec(id, displayName, requiredResearch, unlockGrowableId,
            new ResourceCost(money, items.ToDictionary(x => x.id, x => x.amount)));
    }

    [Serializable] public sealed class UpgradeSettings
    {
        public string id, displayName, requiredResearch;
        public int money;
        public ItemAmount[] items;
        public float growthTimeMultiplier = 1;
        public int yieldBonus;
        public UpgradeSpec ToSpec() => new UpgradeSpec(id, displayName, requiredResearch,
            new ResourceCost(money, items.ToDictionary(x => x.id, x => x.amount)), growthTimeMultiplier, yieldBonus);
    }

    [CreateAssetMenu(menuName = "GrowWhatever/Farm configuration")]
    public sealed class FarmConfiguration : ScriptableObject
    {
        [SerializeField] private int startingMoney = 20;
        [SerializeField] private ItemAmount[] startingSeeds = { new ItemAmount("carrot", 5), new ItemAmount("wheat", 5) };
        [SerializeField, Min(1)] private float autoSaveSeconds = 30;
        [SerializeField] private RecipeSettings[] recipes =
        {
            new RecipeSettings { id = "flour", displayName = "Mill flour", inputId = "wheat", outputId = "flour", outputName = "Flour" },
            new RecipeSettings { id = "parts", displayName = "Forge parts", inputId = "iron", inputAmount = 2,
                outputId = "parts", outputName = "Parts", outputPrice = 16, seconds = 45, requiredResearch = "iron" }
        };
        [SerializeField] private ResearchSettings[] research =
        {
            new ResearchSettings { id = "iron", displayName = "Iron sprout", unlockGrowableId = "iron", money = 20,
                items = new[] { new ItemAmount("carrot", 6), new ItemAmount("flour", 2) } },
            new ResearchSettings { id = "moonflower", displayName = "Moonflower", unlockGrowableId = "moonflower", money = 40,
                requiredResearch = "iron", items = new[] { new ItemAmount("parts", 4), new ItemAmount("flour", 3) } }
        };
        [SerializeField] private UpgradeSettings[] upgrades =
        {
            new UpgradeSettings { id = "growth", displayName = "Growth time -10%", requiredResearch = "iron", money = 40,
                items = new[] { new ItemAmount("parts", 2) }, growthTimeMultiplier = 0.9f },
            new UpgradeSettings { id = "yield", displayName = "Harvest yield +1", requiredResearch = "iron", money = 80,
                items = new[] { new ItemAmount("parts", 4) }, yieldBonus = 1 }
        };

        public float AutoSaveSeconds => autoSaveSeconds;
        public FarmRules CreateRules() => new FarmRules(recipes.Select(x => x.ToSpec()), research.Select(x => x.ToSpec()), upgrades.Select(x => x.ToSpec()));
        public FarmGame CreateGame(IEnumerable<GrowableSpec> definitions) => new FarmGame(definitions, startingMoney,
            startingSeeds.ToDictionary(x => x.id, x => x.amount), CreateRules());
    }
}
