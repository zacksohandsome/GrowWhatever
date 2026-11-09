using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GrowWhatever.Core
{
    /// <summary>Single-player farming rules. The caller settles elapsed time before each command.</summary>
    public sealed class FarmGame
    {
        private readonly Dictionary<string, GrowableSpec> content;
        private readonly Dictionary<string, int> seeds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> produce = new Dictionary<string, int>();
        private readonly FarmPlot[] plots;

        public event Action Changed;
        public int Money { get; private set; }
        public ReadOnlyCollection<FarmPlot> Plots { get; }
        public ReadOnlyCollection<GrowableSpec> Growables { get; }

        public FarmGame(IEnumerable<GrowableSpec> growables, int money,
            IReadOnlyDictionary<string, int> startingSeeds)
        {
            if (money < 0) throw new ArgumentOutOfRangeException(nameof(money));
            var definitions = growables.ToArray();
            if (definitions.Length == 0) throw new ArgumentException("At least one growable is required.");
            content = definitions.ToDictionary(x => x.Id);
            Growables = Array.AsReadOnly(definitions);
            Money = money;
            foreach (var item in definitions)
            {
                int amount = startingSeeds.TryGetValue(item.Id, out int value) ? value : 0;
                if (amount < 0) throw new ArgumentOutOfRangeException(nameof(startingSeeds));
                seeds.Add(item.Id, amount);
                produce.Add(item.Id, 0);
            }
            plots = Enumerable.Range(0, 9).Select(i => new FarmPlot(i / 3, i % 3)).ToArray();
            Plots = Array.AsReadOnly(plots);
        }

        public GrowableSpec Definition(string id) => content[id];
        public int SeedCount(string id) => seeds[id];
        public int ProduceCount(string id) => produce[id];
        public bool IsMature(FarmPlot plot) => !plot.IsEmpty && plot.Progress >= content[plot.GrowableId].GrowthSeconds;
        public int PlantableCount(string id) => Math.Min(plots.Count(x => x.IsEmpty), seeds[id]);
        public int HarvestableCount => plots.Count(x => x.IncludeInBulkHarvest && IsMature(x));

        public bool BuySeed(string id)
        {
            if (!content.TryGetValue(id, out var spec) || Money < spec.SeedPrice || seeds[id] == int.MaxValue) return false;
            Money -= spec.SeedPrice;
            seeds[id]++;
            Changed?.Invoke();
            return true;
        }

        public bool SellSeed(string id) => Sell(id, seeds, content.TryGetValue(id, out var spec) ? spec.SeedPrice : 0);
        public bool SellProduce(string id) => Sell(id, produce, content.TryGetValue(id, out var spec) ? spec.ProducePrice : 0);

        private bool Sell(string id, Dictionary<string, int> inventory, int price)
        {
            if (!inventory.TryGetValue(id, out int amount) || amount <= 0 || price > int.MaxValue - Money) return false;
            inventory[id]--;
            Money += price;
            Changed?.Invoke();
            return true;
        }

        public bool Plant(int index, string id)
        {
            if (index < 0 || index >= plots.Length || !content.ContainsKey(id) || seeds[id] <= 0 || !plots[index].IsEmpty) return false;
            PlantUnchecked(plots[index], id);
            Changed?.Invoke();
            return true;
        }

        public int PlantAll(string id)
        {
            if (!content.ContainsKey(id)) return 0;
            int count = 0;
            foreach (var plot in plots)
            {
                if (!plot.IsEmpty || seeds[id] == 0) continue;
                PlantUnchecked(plot, id);
                count++;
            }
            if (count > 0) Changed?.Invoke();
            return count;
        }

        private void PlantUnchecked(FarmPlot plot, string id)
        {
            seeds[id]--;
            plot.GrowableId = id;
            plot.Progress = 0;
        }

        public bool Harvest(int index)
        {
            if (index < 0 || index >= plots.Length || !HarvestUnchecked(plots[index])) return false;
            Changed?.Invoke();
            return true;
        }

        public int HarvestAll()
        {
            int count = 0;
            foreach (var plot in plots)
                if (plot.IncludeInBulkHarvest && HarvestUnchecked(plot)) count++;
            if (count > 0) Changed?.Invoke();
            return count;
        }

        private bool HarvestUnchecked(FarmPlot plot)
        {
            if (!IsMature(plot)) return false;
            var spec = content[plot.GrowableId];
            if (produce[spec.Id] > int.MaxValue - spec.Yield) return false;
            produce[spec.Id] += spec.Yield;
            plot.GrowableId = null;
            plot.Progress = 0;
            // The bulk setting belongs to the plot and survives this plant's lifetime.
            return true;
        }

        public void SetBulkHarvest(int index, bool include)
        {
            if (index < 0 || index >= plots.Length || plots[index].IncludeInBulkHarvest == include) return;
            plots[index].IncludeInBulkHarvest = include;
            Changed?.Invoke();
        }

        /// <summary>Advance existing work only. Negative elapsed time does not reverse progress.</summary>
        public void Advance(double elapsedSeconds)
        {
            if (elapsedSeconds <= 0 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds)) return;
            bool changed = false;
            foreach (var plot in plots)
            {
                if (plot.IsEmpty || IsMature(plot)) continue;
                plot.Progress = Math.Min(content[plot.GrowableId].GrowthSeconds, plot.Progress + elapsedSeconds);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public bool CanClaimSupply => content.ContainsKey("carrot") && Money < content.Values.Min(x => x.SeedPrice)
            && seeds.Values.All(x => x == 0) && produce.Values.All(x => x == 0) && plots.All(x => x.IsEmpty);

        public bool ClaimSupply()
        {
            if (!CanClaimSupply) return false;
            seeds["carrot"]++;
            Changed?.Invoke();
            return true;
        }
    }
}
