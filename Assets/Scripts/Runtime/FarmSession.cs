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
        [SerializeField] private FarmConfiguration configuration;
        [SerializeField] private FarmAudio audioFeedback;
        [SerializeField] private string saveFileName = "farm-save-v1.json";
        private DateTime lastSettled;
        private float nextRefresh, nextSave;
        private FarmSaveStore saveStore;
        private bool initialized, goalAnnounced;
        public FarmGame Game { get; private set; }
        public string SelectedSeed { get; private set; }
        public string Message { get; private set; } = "Choose a seed, then click an empty plot.";
        public bool NeedsRecovery { get; private set; }
        public string SaveStatus { get; private set; } = "Not saved yet";
        public bool Ready => !NeedsRecovery;
        public string SaveSlot => saveFileName;
        public event Action Changed;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Application.targetFrameRate = 60;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (FarmStandalonePlaythrough.Requested && saveFileName.StartsWith("qa-standalone-"))
                saveFileName = saveFileName.Replace("qa-standalone-", "qa-playthrough-");
#endif
            if (string.IsNullOrWhiteSpace(saveFileName) || System.IO.Path.GetFileName(saveFileName) != saveFileName)
                throw new InvalidOperationException("Save file name must be a local file name.");
            saveStore = new FarmSaveStore(System.IO.Path.Combine(Application.persistentDataPath, saveFileName));
            Game = CreateNewGame();
            var status = saveStore.Read(out var snapshot);
            if (status == SaveReadStatus.Loaded)
            {
                try
                {
                    Game = FarmGame.Restore(growables.Select(x => x.ToSpec()), configuration.CreateRules(), snapshot);
                    Game.Advance(Math.Max(0, (DateTime.UtcNow.Ticks - snapshot.SavedUtcTicks) / (double)TimeSpan.TicksPerSecond));
                    Message = "Welcome back. Existing plants and queued work have progressed.";
                }
                catch (ArgumentException) { NeedsRecovery = true; }
            }
            else if (status != SaveReadStatus.Missing) NeedsRecovery = true;
            if (NeedsRecovery) Message = "Save could not be loaded. Original file is preserved. Choose New farm to restart.";
            SelectedSeed = "carrot";
            lastSettled = DateTime.UtcNow;
            goalAnnounced = Game.IsGoalComplete;
            Game.Changed += NotifyChanged;
            Game.ProcessingCompleted += OnProcessingCompleted;
            nextSave = Time.unscaledTime + (configuration != null ? configuration.AutoSaveSeconds : 30);
            if (!NeedsRecovery) SaveNow();
        }

        private FarmGame CreateNewGame() => configuration != null ? configuration.CreateGame(growables.Select(x => x.ToSpec())) :
            new FarmGame(growables.Select(x => x.ToSpec()), 20, new Dictionary<string, int> { { "carrot", 5 }, { "wheat", 5 } });

        private void OnDestroy()
        {
            if (Game != null)
            {
                Game.Changed -= NotifyChanged;
                Game.ProcessingCompleted -= OnProcessingCompleted;
            }
        }

        private void Update()
        {
            if (!Ready || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.1f;
            SettleTime();
            if (Time.unscaledTime >= nextSave)
            {
                SaveNow();
                nextSave = Time.unscaledTime + (configuration != null ? configuration.AutoSaveSeconds : 30);
                NotifyChanged();
            }
        }

        private void OnApplicationFocus(bool focus)
        {
            if (Game != null && Ready) { SettleTime(); SaveNow(); }
        }

        private void OnApplicationPause(bool pause) { if (Game != null && Ready) { SettleTime(); SaveNow(); } }
        private void OnApplicationQuit() { if (Game != null && Ready) { SettleTime(); SaveNow(); } }

        private void SettleTime()
        {
            var now = DateTime.UtcNow;
            double elapsed = Math.Max(0, (now - lastSettled).TotalSeconds);
            lastSettled = now;
            Game.Advance(elapsed);
        }

        private void NotifyChanged()
        {
            if (!goalAnnounced && Game.IsGoalComplete)
            {
                goalAnnounced = true;
                Message = "First farm goal complete! Keep growing and experimenting.";
                Play(FarmSound.Goal);
            }
            Changed?.Invoke();
        }
        private void OnProcessingCompleted(int count) { Play(FarmSound.Processing); SaveNow(); }
        public void Play(FarmSound sound) { if (audioFeedback != null) audioFeedback.Play(sound); }
        public GrowableDefinition Definition(string id) => Array.Find(growables, x => x.Id == id);

        public bool SaveNow()
        {
            if (NeedsRecovery || Game == null) return false;
            bool saved = saveStore.Write(Game.Capture(lastSettled.Ticks));
            SaveStatus = saved ? "Saved locally" : "Save failed - retry Save";
            return saved;
        }

        public void SaveManually()
        {
            if (!Ready) return;
            SettleTime();
            bool saved = SaveNow();
            Message = saved ? "Farm saved." : "Could not save. Check disk access, then try again.";
            Play(saved ? FarmSound.Click : FarmSound.Unavailable);
            NotifyChanged();
        }

        public void StartNewFarm()
        {
            Game.Changed -= NotifyChanged;
            Game.ProcessingCompleted -= OnProcessingCompleted;
            Game = CreateNewGame();
            Game.Changed += NotifyChanged;
            Game.ProcessingCompleted += OnProcessingCompleted;
            NeedsRecovery = false; goalAnnounced = false;
            lastSettled = DateTime.UtcNow; SelectedSeed = "carrot";
            Message = "New farm started. Choose a seed to plant.";
            SaveNow(); Play(FarmSound.Click); NotifyChanged();
        }

        private void Execute(Func<bool> action, string success, string failure, FarmSound sound)
        {
            if (!Ready) return;
            SettleTime();
            bool result = action();
            Message = Game.IsGoalComplete ? "First farm goal complete! You can keep playing." : result ? success : failure;
            Play(result ? sound : FarmSound.Unavailable);
            SaveNow(); NotifyChanged();
        }

        public void SelectSeed(string id)
        {
            if (!Ready || Definition(id) == null || !Game.IsUnlocked(id)) return;
            SelectedSeed = id;
            Message = $"Selected {Definition(id).DisplayName}. Click an empty plot to plant.";
            Play(FarmSound.Click);
            NotifyChanged();
        }

        public void InteractPlot(int index)
        {
            if (index < 0 || index >= Game.Plots.Count) return;
            bool empty = Game.Plots[index].IsEmpty;
            Execute(() => empty ? Game.Plant(index, SelectedSeed) : Game.Harvest(index), empty ? "Seed planted." : "Harvest collected.",
                empty ? "No seeds left. Buy seeds in the shop." : "Still growing. Mature plants wait for you.", empty ? FarmSound.Sow : FarmSound.Harvest);
        }

        public void ToggleBulk(int index)
        {
            Execute(() => { Game.SetBulkHarvest(index, !Game.Plots[index].IncludeInBulkHarvest); return true; },
                "Bulk harvest setting updated.", "Could not update plot.", FarmSound.Click);
        }

        public void PlantAll() => Execute(() => Game.PlantAll(SelectedSeed) > 0, "Seeds planted.", "No empty plots or seeds.", FarmSound.Sow);
        public void HarvestAll() => Execute(() => Game.HarvestAll() > 0, "Harvest collected.", "No included mature plots.", FarmSound.Harvest);
        public void BuySeed(string id) => Execute(() => Game.BuySeed(id), "Seed purchased.", "Not enough coins or seed is locked.", FarmSound.Trade);
        public void SellSeed(string id) => Execute(() => Game.SellSeed(id), "Seed sold at full price.", "No seed to sell.", FarmSound.Trade);
        public void SellProduce(string id) => Execute(() => Game.SellProduce(id), "Item sold.", "No item to sell.", FarmSound.Trade);
        public void ClaimSupply() => Execute(Game.ClaimSupply, "One free carrot seed received.", "You still have usable resources.", FarmSound.Click);
        public void QueueRecipe(string id) => Execute(() => Game.QueueRecipe(id), "Work queued; materials paid.", "Queue full, locked recipe or insufficient materials.", FarmSound.Click);
        public void Research(string id) => Execute(() => Game.Research(id), "Research complete. New seeds are available.", "Research unavailable or resources insufficient.", FarmSound.Unlock);
        public void Upgrade(string id) => Execute(() => Game.Upgrade(id), "Permanent upgrade purchased.", "Upgrade unavailable or resources insufficient.", FarmSound.Unlock);

        public string CurrentGoal
        {
            get
            {
                if (NeedsRecovery) return "Save needs attention. Use New farm to explicitly restart.";
                if (!Game.IsResearched("iron")) return "Research Iron sprout: collect carrots, mill flour and earn coins.";
                if (!Game.IsResearched("moonflower")) return "Research Moonflower: forge parts, mill flour and earn coins.";
                if (!Game.Rules.Upgrades.Any(x => Game.IsUpgraded(x.Id))) return "Purchase a growing or harvest upgrade in the Genetic Lab.";
                if (!Game.UsedNeighborBoost) return "Plant Moonflower next to a growing non-Moonflower plot.";
                if (!Game.HarvestedMoonEssence) return "Manually harvest a mature Moonflower to collect its essence.";
                return "FIRST FARM GOAL COMPLETE / Keep growing and experimenting.";
            }
        }
    }
}
