#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using GrowWhatever.QA;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GrowWhatever
{
    internal static class FarmStandalonePlaythrough
    {
        internal static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-growwhatever-playthrough") >= 0;
        [Serializable] private sealed class Report
        {
            public bool completed, passed;
            public long startedUtcTicks;
            public double elapsedSeconds, firstHarvestSeconds, ironResearchSeconds, moonResearchSeconds;
            public int actions, money, frames;
            public string failure;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start() { if (Requested) Run(); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

        private static void Click(string path)
        {
            var target = GameObject.Find(path);
            Check(target != null, "Missing active UI target.");
            var button = target.GetComponent<UnityEngine.UI.Button>();
            Check(button != null && button.IsInteractable(), "UI command unavailable.");
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        }

        private static void Panel(string name, string button)
        {
            var panel = GameObject.Find("Farm Canvas").transform.Find(name);
            if (!panel.gameObject.activeSelf) Click("Farm Canvas/Header/" + button);
        }

        private static void Apply(FarmSession session, FarmPlayerAction action)
        {
            string plot = $"Farm Canvas/Garden/Plots - sorted by ground row/Plot {action.Plot / 3 + 1}-{action.Plot % 3 + 1}";
            switch (action.Kind)
            {
                case FarmActionKind.Harvest: Click(plot); break;
                case FarmActionKind.ToggleBulk: Click(plot + "/BulkButton"); break;
                case FarmActionKind.Research:
                case FarmActionKind.Upgrade:
                    Panel("Genetic Lab Panel", "LabButton");
                    Click("Farm Canvas/Genetic Lab Panel/" + action.Id + " Purchase/PurchaseButton"); break;
                case FarmActionKind.Queue:
                    Panel("Processing Panel", "ProcessingButton");
                    Click("Farm Canvas/Processing Panel/" + action.Id + " Recipe/QueueButton"); break;
                case FarmActionKind.Sell:
                    if (action.Id == "flour" || action.Id == "parts")
                    {
                        Panel("Processing Panel", "ProcessingButton");
                        Click("Farm Canvas/Processing Panel/" + action.Id + " Recipe/SellButton");
                    }
                    else
                    {
                        Panel("Seed Shop Panel", "ShopButton");
                        Click("Farm Canvas/Seed Shop Panel/" + session.Definition(action.Id).DisplayName + " Shop Row/SellProduceButton");
                    }
                    break;
                case FarmActionKind.Buy:
                    Panel("Seed Shop Panel", "ShopButton");
                    Click("Farm Canvas/Seed Shop Panel/" + session.Definition(action.Id).DisplayName + " Shop Row/BuyButton"); break;
                case FarmActionKind.Plant:
                    if (session.SelectedSeed != action.Id)
                    {
                        Panel("Seed Shop Panel", "ShopButton");
                        Click("Farm Canvas/Seed Shop Panel/" + session.Definition(action.Id).DisplayName + " Shop Row/SelectButton");
                    }
                    Click(plot); break;
            }
        }

        private static async void Run()
        {
            string path = null;
            var report = new Report { startedUtcTicks = DateTime.UtcNow.Ticks };
            try
            {
                await Task.Delay(500);
                var session = UnityEngine.Object.FindFirstObjectByType<FarmSession>();
                Check(session != null && session.Ready && session.SaveSlot.StartsWith("qa-playthrough-"), "Invalid QA session.");
                path = Path.Combine(Application.persistentDataPath, session.SaveSlot + ".playthrough.json");
                Check(session.Game.Money == 20 && session.Game.SeedCount("carrot") == 5, "Expected fresh initial resources.");
                while (!session.Game.IsGoalComplete)
                {
                    report.elapsedSeconds = (DateTime.UtcNow.Ticks - report.startedUtcTicks) / (double)TimeSpan.TicksPerSecond;
                    Check(report.elapsedSeconds < 2700, "Playthrough exceeded 45 minutes.");
                    for (int i = 0; i < 12 && !session.Game.IsGoalComplete; i++)
                    {
                        var action = FarmPlaythroughPolicy.Decide(session.Game);
                        if (action.Kind == FarmActionKind.None) break;
                        Apply(session, action); report.actions++;
                        if (action.Kind == FarmActionKind.Harvest && report.firstHarvestSeconds == 0) report.firstHarvestSeconds = report.elapsedSeconds;
                    }
                    if (session.Game.IsUnlocked("iron") && report.ironResearchSeconds == 0) report.ironResearchSeconds = report.elapsedSeconds;
                    if (session.Game.IsUnlocked("moonflower") && report.moonResearchSeconds == 0) report.moonResearchSeconds = report.elapsedSeconds;
                    report.frames = Time.frameCount; report.money = session.Game.Money;
                    File.WriteAllText(path, JsonUtility.ToJson(report, true));
                    await Task.Delay(1000);
                }
                report.completed = true; report.passed = session.Game.IsGoalComplete && session.SaveNow();
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Application.Quit(report.passed ? 0 : 1);
            }
            catch (Exception e)
            {
                report.failure = e is InvalidOperationException ? e.Message : "Unexpected QA failure; raw details suppressed.";
                if (path != null) File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Application.Quit(1);
            }
        }
    }
}
#endif
