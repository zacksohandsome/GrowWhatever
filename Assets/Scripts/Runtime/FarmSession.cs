using System;
using System.Collections.Generic;
using System.Linq;
using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    /// <summary>Owns the local session and clock. Views issue commands through this boundary.</summary>
    public sealed class FarmSession : MonoBehaviour
    {
        [SerializeField] private GrowableDefinition[] growables;
        private DateTime lastSettled;
        private float nextRefresh;
        public FarmGame Game { get; private set; }
        public string SelectedSeed { get; private set; }
        public string Message { get; private set; } = "Choose a seed, then click an empty plot.";
        public event Action Changed;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (Game != null) return;
            Game = new FarmGame(growables.Select(x => x.ToSpec()), 20,
                new Dictionary<string, int> { { "carrot", 5 }, { "wheat", 5 } });
            SelectedSeed = "carrot";
            lastSettled = DateTime.UtcNow;
            Game.Changed += NotifyChanged;
        }

        private void OnDestroy()
        {
            if (Game != null) Game.Changed -= NotifyChanged;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            SettleTime();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (Game != null) SettleTime();
        }

        private void SettleTime()
        {
            var now = DateTime.UtcNow;
            double elapsed = Math.Max(0, (now - lastSettled).TotalSeconds);
            lastSettled = now;
            Game.Advance(elapsed);
        }

        private void NotifyChanged() => Changed?.Invoke();
        public GrowableDefinition Definition(string id) => Array.Find(growables, x => x.Id == id);

        public void SelectSeed(string id)
        {
            if (Definition(id) == null) return;
            SelectedSeed = id;
            Message = $"Selected {Definition(id).DisplayName}. Click an empty plot to plant.";
            NotifyChanged();
        }

        public void InteractPlot(int index)
        {
            SettleTime();
            var plot = Game.Plots[index];
            if (plot.IsEmpty)
                Message = Game.Plant(index, SelectedSeed) ? "Seed planted." : "No seeds left. Buy seeds in the shop.";
            else
                Message = Game.Harvest(index) ? "Harvest collected." : "Still growing. Mature plants wait for you.";
            NotifyChanged();
        }

        public void ToggleBulk(int index)
        {
            SettleTime();
            Game.SetBulkHarvest(index, !Game.Plots[index].IncludeInBulkHarvest);
        }

        public void PlantAll() { SettleTime(); Message = $"Planted {Game.PlantAll(SelectedSeed)} plots."; NotifyChanged(); }
        public void HarvestAll() { SettleTime(); Message = $"Harvested {Game.HarvestAll()} plots."; NotifyChanged(); }
        public void BuySeed(string id) { SettleTime(); Message = Game.BuySeed(id) ? "Seed purchased." : "Not enough coins."; NotifyChanged(); }
        public void SellSeed(string id) { SettleTime(); Message = Game.SellSeed(id) ? "Seed sold at full price." : "No seed to sell."; NotifyChanged(); }
        public void SellProduce(string id) { SettleTime(); Message = Game.SellProduce(id) ? "Produce sold." : "No produce to sell."; NotifyChanged(); }
        public void ClaimSupply() { SettleTime(); Message = Game.ClaimSupply() ? "One free carrot seed received." : "You still have usable resources."; NotifyChanged(); }
    }
}
