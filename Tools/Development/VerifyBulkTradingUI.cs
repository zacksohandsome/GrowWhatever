// Play Mode integration QA. Requires an isolated qa-bulk-* save slot.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrowWhatever;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

void Require(bool condition, string message)
{ if (!condition) throw new InvalidOperationException(message); }

async Task Frame()
{
    UnityEditor.EditorApplication.isPaused = true;
    UnityEditor.EditorApplication.Step();
    await Task.Delay(100);
    Canvas.ForceUpdateCanvases();
}

GameObject Raycast(Vector2 position)
{
    var data = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
    Require(hits.Count > 0,"UI raycast missed");
    return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
}

async Task Click(string path)
{
    await Frame();
    var target = GameObject.Find(path);
    Require(target != null,"Missing target: " + path);
    Require(target.GetComponent<UnityEngine.UI.Button>().IsInteractable(),"Unavailable target: " + path);
    var position = RectTransformUtility.WorldToScreenPoint(null,target.transform.position);
    Require(Raycast(position) == target,"Occluded target: " + path);
    ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current) { position=position, button=PointerEventData.InputButton.Left },ExecuteEvents.pointerClickHandler);
}

Require(UnityEditor.EditorApplication.isPlaying,"Play Mode required");
var session = UnityEngine.Object.FindFirstObjectByType<FarmSession>();
Require(session.SaveSlot.StartsWith("qa-bulk-"),"An isolated QA save is required");
var game = session.Game;
Require(game.Money == 20 && game.SeedCount("carrot") == 5,"Fresh QA state required");
var canvas = GameObject.Find("Farm Canvas").transform;
var modal = canvas.Find("Trade Quantity").GetComponent<TradeQuantityView>();
var input = modal.GetComponentInChildren<TMP_InputField>(true);
string shop = "Farm Canvas/Seed Shop Panel/Carrot Shop Row/";
string dialog = "Farm Canvas/Trade Quantity/Dialog/";
int startFrame = Time.frameCount;
await Click(shop+"BuyButton"); await Click(shop+"SellSeedButton");
Require(game.Money == 20 && game.SeedCount("carrot") == 5,"Single trades regressed");
await Click(shop+"BulkBuyButton");
Require(modal.IsOpen && input.text == "1","Initial quantity must be one");
input.text = "3"; input.onEndEdit.Invoke(input.text);
await Click(dialog+"plusTenButton"); Require(input.text == "10","+10 did not clamp to budget");
await Click(dialog+"minusTenButton"); Require(input.text == "0","-10 did not reach zero");
Require(!GameObject.Find(dialog+"ConfirmButton").GetComponent<UnityEngine.UI.Button>().IsInteractable(),"Zero trade is enabled");
await Click(dialog+"plusOneButton"); Require(input.text == "1","+1 failed");
await Click(dialog+"minimumButton"); Require(input.text == "0","MIN failed");
await Click(dialog+"maximumButton"); Require(input.text == "10","MAX failed");
input.text = "3"; input.onEndEdit.Invoke(input.text);
var confirm = GameObject.Find(dialog+"ConfirmButton").GetComponent<UnityEngine.UI.Button>();
await Click(dialog+"ConfirmButton");
confirm.onClick.Invoke(); // A queued duplicate callback must also be harmless after closing.
Require(!modal.IsOpen && game.Money == 14 && game.SeedCount("carrot") == 8,"Bulk purchase or duplicate protection failed");
await Click(shop+"BulkSellSeedButton");
input.text = "3"; input.onEndEdit.Invoke(input.text); await Click(dialog+"ConfirmButton");
Require(game.Money == 20 && game.SeedCount("carrot") == 5,"Bulk seed refund failed");

await Click(shop+"BulkBuyButton"); await Frame();
int before = game.Money;
var underlyingBuy = GameObject.Find(shop+"BuyButton");
Require(!underlyingBuy.GetComponent<UnityEngine.UI.Button>().IsInteractable(),"Background navigation remained enabled");
var outsidePosition = RectTransformUtility.WorldToScreenPoint(null,underlyingBuy.transform.position);
var outsideReceiver = Raycast(outsidePosition);
Require(outsideReceiver == modal.gameObject,"Outside click would reach background");
ExecuteEvents.Execute(outsideReceiver,new PointerEventData(EventSystem.current) { position=outsidePosition, button=PointerEventData.InputButton.Left },ExecuteEvents.pointerClickHandler);
Require(!modal.IsOpen && game.Money == before,"Outside close changed resources");
Require(underlyingBuy.GetComponent<UnityEngine.UI.Button>().IsInteractable(),"Background was not restored");
await Click(shop+"BulkBuyButton");
await Click(dialog+"CancelButton"); Require(!modal.IsOpen && game.Money == before,"Cancel failed");
await Click(shop+"BulkBuyButton");
// Exercise the EventSystem cancel route; physical keyboard polling is separate.
ExecuteEvents.Execute(modal.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);
Require(!modal.IsOpen && game.Money == before,"Cancel event did not close safely");

game.PlantAll("carrot"); game.Advance(30); game.HarvestAll();
await Click(shop+"BulkSellProduceButton");
input.text = "999999999999999999999999999999999"; input.onEndEdit.Invoke(input.text);
Require(input.text == "15","Huge numeric input did not clamp");
input.text = "3"; input.onEndEdit.Invoke(input.text);
await Click(dialog+"minusTenButton"); Require(input.text == "0","-10 below zero did not clamp");
input.text = "3"; input.onEndEdit.Invoke(input.text); await Click(dialog+"ConfirmButton");
Require(game.Money == 23 && game.ProduceCount("carrot") == 12,"Bulk harvest sale failed");

// Keep growth progressing while the modal blocks player commands.
game.PlantAll("wheat"); await Click(shop+"BulkBuyButton");
double progress = game.Plots.First(x=>!x.IsEmpty).Progress;
game.Advance(1);
Require(game.Plots.First(x=>!x.IsEmpty).Progress > progress,"Modal paused the game");
modal.Close(); game.Advance(60); game.HarvestAll();
game.QueueRecipe("flour"); game.QueueRecipe("flour"); game.QueueRecipe("flour"); game.Advance(90);
await Click("Farm Canvas/Header/ProcessingButton");
await Click("Farm Canvas/Processing Panel/flour Recipe/BulkSellButton");
input.text = "2"; input.onEndEdit.Invoke(input.text); await Click(dialog+"ConfirmButton");
Require(game.Money == 41 && game.ProduceCount("flour") == 1,"Processed-item batch sale failed");
await Click("Farm Canvas/Processing Panel/flour Recipe/BulkSellButton");
Require(input.text == "1","Quantity was remembered between trades");
await Click(dialog+"ConfirmButton");
Require(game.ProduceCount("flour") == 0,"Sell-all did not complete in one confirmation");
Require(Time.frameCount > startFrame,"No real player frames advanced");
Require(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,"Expected exactly one EventSystem");
await Click("Farm Canvas/Header/ShopButton");
await Click(shop+"BulkSellProduceButton");
input.text = "12"; input.onEndEdit.Invoke(input.text);
// Simulate a resource change before its view notification is delivered.
var inventory = (Dictionary<string,int>)typeof(GrowWhatever.Core.FarmGame)
    .GetField("produce",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(game);
inventory["carrot"] = 9;
before = game.Money;
await Click(dialog+"ConfirmButton");
Require(modal.IsOpen && input.text == "9" && game.Money == before && game.ProduceCount("carrot") == 9,
    "Stale confirmation partially traded or failed to update quantity");
Require(!string.IsNullOrEmpty(modal.transform.Find("Dialog/Feedback").GetComponent<TMP_Text>().text),"Missing failure feedback");
modal.Close();
await Click(shop+"BulkBuyButton");
await Frame();
return new { passed=true, fourTradeKinds=true, outsideClickConsumed=true, cancelEventCloses=true, numericClamp=true,
    duplicateConfirmationBlocked=true, staleConfirmationRejected=true,
    framesAdvanced=Time.frameCount-startFrame, money=game.Money, isolatedSave=true };
