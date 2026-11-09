// M1 integration smoke test. Run in a fresh Play session, never in Edit Mode.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

void Click(string path)
{
    var target = GameObject.Find(path);
    Require(target != null, "Missing UI target: " + path);
    var button = target.GetComponent<UnityEngine.UI.Button>();
    Require(button != null && button.IsInteractable(), "Button unavailable: " + path);
    var data = new PointerEventData(EventSystem.current);
    data.button = PointerEventData.InputButton.Left;
    data.position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
    var hits = new List<RaycastResult>();
    EventSystem.current.RaycastAll(data, hits);
    Require(hits.Count > 0, "UI raycast missed: " + path);
    var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
    Require(receiver == target, "Click intercepted: " + path);
    ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
}

Require(UnityEditor.EditorApplication.isPlaying, "Start Play mode first.");
var session = UnityEngine.Object.FindFirstObjectByType<GrowWhatever.FarmSession>();
var game = session.Game;
Require(game.Money == 20 && game.SeedCount("carrot") == 5, "Expected fresh session.");
Canvas.ForceUpdateCanvases();
int startingFrame = Time.frameCount;
Click("Farm Canvas/Seed Shop Panel/Carrot Shop Row/BuyButton");
Require(game.Money == 18 && game.SeedCount("carrot") == 6, "Purchase did not update state.");
Click("Farm Canvas/Seed Shop Panel/Carrot Shop Row/SellSeedButton");
Require(game.Money == 20 && game.SeedCount("carrot") == 5, "Seed refund mismatch.");
Click("Farm Canvas/Action Bar/PlantAllButton");
Require(game.Plots.Count(x => !x.IsEmpty) == 5 && game.SeedCount("carrot") == 0, "Bulk planting mismatch.");
Click("Farm Canvas/Garden/Plots - sorted by ground row/Plot 1-1/BulkButton");
Require(!game.Plots[0].IncludeInBulkHarvest, "Bulk exclusion did not apply.");
Require(!game.Harvest(0), "Immature plant was harvested.");
// Wait for real time; no time scale or model-progress adjustment.
await System.Threading.Tasks.Task.Delay(31000);
Require(Time.frameCount > startingFrame, "Player loop did not advance.");
Require(game.Plots.Take(5).All(game.IsMature), "Real-time growth failed.");
Click("Farm Canvas/Action Bar/HarvestAllButton");
Require(game.ProduceCount("carrot") == 12 && !game.Plots[0].IsEmpty, "Bulk harvest ignored exclusion.");
Click("Farm Canvas/Garden/Plots - sorted by ground row/Plot 1-1");
Require(game.ProduceCount("carrot") == 15 && game.Plots[0].IsEmpty, "Manual harvest failed.");
Require(!game.Harvest(0) && game.ProduceCount("carrot") == 15, "Repeated harvest duplicated produce.");
Click("Farm Canvas/Seed Shop Panel/Carrot Shop Row/SellProduceButton");
Require(game.Money == 21 && game.ProduceCount("carrot") == 14, "Produce sale mismatch.");
Click("Farm Canvas/Seed Shop Panel/Wheat Shop Row/SelectButton");
Click("Farm Canvas/Garden/Plots - sorted by ground row/Plot 1-1");
Require(game.Plots[0].GrowableId == "wheat" && !game.Plots[0].IncludeInBulkHarvest, "Replant lost bulk setting.");
Click("Farm Canvas/Header/ShopButton");
Require(!GameObject.Find("Farm Canvas").transform.Find("Seed Shop Panel").gameObject.activeSelf, "Shop did not close.");
Click("Farm Canvas/Header/ShopButton");
Require(GameObject.Find("Farm Canvas/Seed Shop Panel") != null, "Shop did not reopen.");
return new { passed = true, realWaitSeconds = 31, framesAdvanced = Time.frameCount - startingFrame,
    money = game.Money, carrotProduce = game.ProduceCount("carrot"), replanted = game.Plots[0].GrowableId,
    bulkSettingPreserved = !game.Plots[0].IncludeInBulkHarvest };
