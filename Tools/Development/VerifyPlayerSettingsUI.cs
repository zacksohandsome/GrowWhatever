// Play Mode integration QA. Start with isolated qa-settings-* farm and preferences slots.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GrowWhatever;
using GrowWhatever.Core;
using UnityEngine;
using UnityEngine.EventSystems;

void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
async Task Frame() { await Task.Delay(80); Canvas.ForceUpdateCanvases(); }
GameObject Hit(Vector2 point)
{
    var data=new PointerEventData(EventSystem.current){position=point};
    var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
    Require(hits.Count>0,"UI raycast missed");
    return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
}
async Task Click(string path)
{
    await Frame(); var obj=GameObject.Find(path); Require(obj!=null,"Missing QA button");
    Require(obj.GetComponent<UnityEngine.UI.Button>().IsInteractable(),"Button unavailable");
    var point=RectTransformUtility.WorldToScreenPoint(null,obj.transform.position);
    Require(Hit(point)==obj,"Button is occluded");
    ExecuteEvents.Execute(obj,new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
}
var session=UnityEngine.Object.FindFirstObjectByType<FarmSession>();
Require(session.SaveSlot.StartsWith("qa-settings-"),"Isolated farm slot required");
var service=UnityEngine.Object.FindFirstObjectByType<PlayerSettingsService>();
var slot=new UnityEditor.SerializedObject(service).FindProperty("settingsFileName").stringValue;
Require(slot.StartsWith("qa-settings-preferences-"),"Isolated preferences slot required");
var view=UnityEngine.Object.FindFirstObjectByType<PlayerSettingsView>();
int startFrame=Time.frameCount;
await Click("Farm Canvas/Garden/SettingsButton");
await Frame();
Require(view.IsOpen,"Settings did not open");
string dialog="Farm Canvas/Player Settings/Dialog/";
var garden=GameObject.Find("Farm Canvas/Garden").GetComponent<CanvasGroup>();
Require(!garden.interactable&&!garden.blocksRaycasts,"Background input remains enabled");
var underlying=GameObject.Find("Farm Canvas/Garden/NewFarmButton");
Require(Hit(RectTransformUtility.WorldToScreenPoint(null,underlying.transform.position))!=underlying,"Background click escaped overlay");
var master=GameObject.Find(dialog+"MasterSlider").GetComponent<UnityEngine.UI.Slider>();
var effects=GameObject.Find(dialog+"EffectsSlider").GetComponent<UnityEngine.UI.Slider>();
master.value=0.5f; effects.value=0;
var audio=GameObject.Find("Farm Audio").GetComponent<AudioSource>();
Require(Math.Abs(audio.volume)<0.001,"Zero effects did not mute audio");
effects.value=0.4f;
Require(Math.Abs(audio.volume-0.2f)<0.001,"Master and effects gains did not multiply");
await Click(dialog+"ModeButton"); await Click(dialog+"ApplyButton");
Require(service.IsPreviewing,"Display preview not started");
Require(!GameObject.Find(dialog).GetComponent<CanvasGroup>().interactable,"Preview did not block settings edits");
view.OnCancel(new BaseEventData(EventSystem.current)); await Frame();
Require(!service.IsPreviewing&&view.IsOpen,"Cancel did not revert preview and stay open");
Require(service.Values.Master==0.5f,"Reverting display discarded audio");
await Click(dialog+"DefaultsButton");
await Click("Farm Canvas/Player Settings/Confirmation/Dialog/RevertButton");
Require(service.Values.Master==0.5f,"Cancel defaults changed audio");
await Click(dialog+"DefaultsButton");
await Click("Farm Canvas/Player Settings/Confirmation/Dialog/KeepButton");
Require(service.IsPreviewing&&service.Values.Master==1&&service.Values.Effects==0.7f,"Defaults were not previewed with immediate audio");
await Click("Farm Canvas/Player Settings/Confirmation/Dialog/KeepButton");
Require(!service.IsPreviewing,"Keep did not confirm preview");
await Click(dialog+"ModeButton"); await Click(dialog+"CloseButton");
Require(!view.IsOpen&&garden.interactable&&garden.blocksRaycasts,"Closing did not restore background");
await Click("Farm Canvas/Garden/SettingsButton");
Require(GameObject.Find(dialog+"ModeButton/Label").GetComponent<TMPro.TMP_Text>().text=="Windowed","Unapplied display choice was retained");
view.Close(); await Frame();
await Click("Farm Canvas/Garden/NewFarmButton");
var hud=UnityEngine.Object.FindFirstObjectByType<FarmHudView>();
Require(hud.HasConfirmation,"New farm dialog did not open");
ExecuteEvents.ExecuteHierarchy(GameObject.Find("Farm Canvas/New Farm Confirmation/Dialog/CancelButton"),new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);
Require(!hud.HasConfirmation&&!view.IsOpen,"Cancel did not prioritize new farm confirmation");
view.OnCancel(new BaseEventData(EventSystem.current));
Require(!view.IsOpen,"Same-frame cancel also opened settings");
await Frame();
var trade=UnityEngine.Object.FindFirstObjectByType<TradeQuantityView>(FindObjectsInactive.Include);
trade.Open("carrot",FarmTradeKind.BuySeed); await Frame();
ExecuteEvents.ExecuteHierarchy(trade.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);
view.OnCancel(new BaseEventData(EventSystem.current));
Require(!trade.IsOpen&&!view.IsOpen,"Trade cancel also opened settings");
await Frame();
ExecuteEvents.ExecuteHierarchy(GameObject.Find("Farm Canvas/Garden/SettingsButton"),new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);
Require(view.IsOpen,"Cancel action did not open settings");
await Frame();
Require(Time.frameCount>startFrame,"Player frames did not advance");
return new { passed=true,raycastClicks=true,backgroundBlocked=true,audioGain=true,previewCancel=true,defaultsConfirmCancel=true,unappliedDiscarded=true,cancelPriority=true,frames=Time.frameCount-startFrame };
