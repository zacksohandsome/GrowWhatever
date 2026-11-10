// Run with script eval in Play Mode. Uses its own synthetic save slot.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GrowWhatever;
using GrowWhatever.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

Require(Application.isPlaying, "Start Play Mode first.");
var host = new GameObject("Synthetic time skip verification");
host.SetActive(false);
var session = host.AddComponent<FarmSession>();
string fileName = "qa-time-skip-" + Guid.NewGuid().ToString("N") + ".json";
string savePath = Path.Combine(Application.persistentDataPath, fileName);
UnityEditor.Editor inspector = null;
try
{
    void Set(string name, object value) => typeof(FarmSession).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, value);
    var definitions = new[] { "Carrot", "Wheat", "IronSprout", "Moonflower" }
        .Select(name => AssetDatabase.LoadAssetAtPath<GrowableDefinition>("Assets/Data/Growables/" + name + ".asset")).ToArray();
    var config = AssetDatabase.LoadAssetAtPath<FarmConfiguration>("Assets/Data/FarmConfiguration.asset");
    Set("growables", definitions);
    Set("configuration", config);
    Set("saveFileName", fileName);
    Require(!session.DebugSkipTime(1), "Uninitialized session accepted a skip.");
    session.Initialize();
    var game = session.Game;
    game.Plant(0, "wheat");
    game.Plant(1, "wheat");
    game.Advance(game.Definition("wheat").GrowthSeconds);
    game.HarvestAll();
    Require(game.QueueRecipe("flour") && game.QueueRecipe("flour"), "Could not prepare two processing jobs.");
    game.Plant(0, "carrot");

    foreach (double invalid in new[] { 0d, -1, double.NaN, double.PositiveInfinity, double.MaxValue })
        Require(!session.DebugSkipTime(invalid), "Invalid duration accepted.");
    Require(game.Plots[0].Progress == 0, "Invalid skips changed growth.");

    int notifications = 0;
    session.Changed += () => notifications++;
    double firstJobSeconds = game.Jobs[0].RemainingSeconds;
    double partialSeconds = firstJobSeconds / 2;
    Set("lastSettled", DateTime.UtcNow);
    Require(session.DebugSkipTime(partialSeconds / 60), "Partial skip rejected.");
    Require(game.Jobs.Count == 2 && Math.Abs(game.Jobs[0].RemainingSeconds - partialSeconds) < 0.5,
        "Minutes conversion or partial queue progress incorrect.");
    Require(notifications > 0, "Views were not notified.");

    // Exercise the Inspector's real button callback and serialized minutes field.
    double finishSeconds = Math.Max(firstJobSeconds * 2, game.Definition("carrot").GrowthSeconds);
    Set("debugSkipMinutes", finishSeconds / 60);
    inspector = UnityEditor.Editor.CreateEditor(session);
    var root = inspector.CreateInspectorGUI();
    root.Bind(inspector.serializedObject);
    root.Q<DoubleField>().value = finishSeconds / 60;
    var button = root.Query<Button>().ToList().Single(x => x.text == "Skip Time");
    // This tree has no panel, so dispatch through Clickable rather than a panel event.
    typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(button.clickable, new object[] { null, 0 });
    Require(game.Jobs.Count == 0 && game.ProduceCount("flour") == 2, "Inspector button did not finish queued work.");
    Require(game.IsMature(game.Plots[0]) && game.ProduceCount("carrot") == 0, "Growth did not cap at maturity.");

    var store = new FarmSaveStore(savePath);
    Require(store.Read(out var snapshot) == SaveReadStatus.Loaded, "Skipped progress was not saved.");
    var restored = FarmGame.Restore(definitions.Select(x => x.ToSpec()), config.CreateRules(), snapshot);
    Require(restored.Jobs.Count == 0 && restored.ProduceCount("flour") == 2 && restored.IsMature(restored.Plots[0]),
        "Saved progress did not survive reload.");
    Require(snapshot.SavedUtcTicks <= DateTime.UtcNow.Ticks, "Skip moved the saved clock into the future.");

    Set("<NeedsRecovery>k__BackingField", true);
    Require(!session.DebugSkipTime(1), "Recovery session accepted a skip.");
    return new { passed = true, partialSkip = true, inspectorButton = true, queueCompleted = true,
        maturityCapped = true, saveReload = true, invalidInputsRejected = true, recoveryBlocked = true };
}
finally
{
    if (inspector != null) UnityEngine.Object.DestroyImmediate(inspector);
    UnityEngine.Object.DestroyImmediate(host);
    if (File.Exists(savePath)) File.Delete(savePath);
    if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
}
