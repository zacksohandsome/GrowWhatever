#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace GrowWhatever
{
    // Opt-in local QA only; excluded from the release build. No network or command server.
    internal static class FarmStandaloneCheck
    {
        [Serializable] private sealed class Result
        { public int phase; public bool passed; public int money; public int flour; public double savedGrowth; public string failure; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-growwhatever-qa") >= 0) Run();
        }

        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        private static async void Run()
        {
            string resultPath = null;
            try
            {
                await Task.Delay(500);
                var session = UnityEngine.Object.FindFirstObjectByType<FarmSession>();
                Check(session != null && session.Ready, "Session could not load.");
                Check(session.SaveSlot.StartsWith("qa-standalone-"), "Refusing to run QA on a regular save slot.");
                resultPath = Path.Combine(Application.persistentDataPath, session.SaveSlot + ".result.json");
                var game = session.Game;
                if (!File.Exists(resultPath))
                {
                    Check(game.Money == 20 && game.SeedCount("carrot") == 5, "Expected a new QA farm.");
                    session.BuySeed("carrot"); session.SellSeed("carrot");
                    Check(game.Money == 20, "Trade mismatch.");
                    session.InteractPlot(0); session.SelectSeed("wheat"); session.InteractPlot(1);
                    var deadline = DateTime.UtcNow.AddSeconds(70);
                    while (!game.IsMature(game.Plots[1]) && DateTime.UtcNow < deadline) await Task.Delay(500);
                    Check(game.IsMature(game.Plots[0]) && game.IsMature(game.Plots[1]), "Real-time crop growth failed.");
                    session.InteractPlot(0); session.InteractPlot(1);
                    Check(game.ProduceCount("carrot") == 3 && game.ProduceCount("wheat") == 3, "Harvest mismatch.");
                    session.QueueRecipe("flour"); session.InteractPlot(0); session.ToggleBulk(0);
                    Check(game.Jobs.Count == 1 && game.Plots[0].GrowableId == "wheat", "Could not prepare offline work.");
                    Check(session.SaveNow(), "Save failed.");
                    File.WriteAllText(resultPath, JsonUtility.ToJson(new Result { phase = 1, passed = true, money = game.Money }));
                }
                else
                {
                    var previous = JsonUtility.FromJson<Result>(File.ReadAllText(resultPath));
                    Check(previous.phase == 1 && previous.passed, "Invalid QA continuation marker.");
                    Check(game.Jobs.Count == 0 && game.ProduceCount("flour") == 1, "Offline processing failed or duplicated.");
                    Check(game.Plots[0].GrowableId == "wheat" && game.Plots[0].Progress > 20, "Offline growth failed.");
                    Check(!game.Plots[0].IncludeInBulkHarvest && game.Money == 20, "Save did not preserve state.");
                    Check(session.SaveNow(), "Second save failed.");
                    File.WriteAllText(resultPath, JsonUtility.ToJson(new Result { phase = 2, passed = true, money = game.Money,
                        flour = game.ProduceCount("flour"), savedGrowth = game.Plots[0].Progress }));
                }
                Application.Quit(0);
            }
            catch (Exception)
            {
                if (resultPath != null) File.WriteAllText(resultPath, JsonUtility.ToJson(new Result { passed = false, failure = "Standalone QA failed; inspect synthetic test state locally." }));
                Application.Quit(1);
            }
        }
    }
}
#endif
