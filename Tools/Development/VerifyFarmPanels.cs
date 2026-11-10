using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

async Task Click(string path)
{
    // In an inactive desktop session the Editor can stop its native player loop.
    // Step one real frame so newly activated graphics participate in raycasting.
    UnityEditor.EditorApplication.isPaused=true;
    UnityEditor.EditorApplication.Step();
    await Task.Delay(150);
    var target=GameObject.Find(path);
    if(target==null)throw new InvalidOperationException("UI target is not active: "+path);
    var button=target.GetComponent<UnityEngine.UI.Button>();
    if(button==null || !button.IsInteractable())throw new InvalidOperationException("UI action unavailable: "+path);
    Canvas.ForceUpdateCanvases();
    var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
        position=RectTransformUtility.WorldToScreenPoint(null,target.transform.position)};
    var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(data,hits);
    if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=target)
        throw new InvalidOperationException("UI target is occluded: "+path);
    ExecuteEvents.Execute(target,data,ExecuteEvents.pointerClickHandler);
}

var session=UnityEngine.Object.FindFirstObjectByType<GrowWhatever.FarmSession>();
if(!session.Ready || session.Game.Growables.Count!=4)throw new InvalidOperationException("Expanded session did not load.");
var shop=GameObject.Find("Farm Canvas").transform.Find("Seed Shop Panel");
if(shop.childCount!=6)throw new InvalidOperationException("Expected header plus four shop rows.");
if(shop.Find("Iron sprout Shop Row/BuyButton").GetComponent<UnityEngine.UI.Button>().IsInteractable())
    throw new InvalidOperationException("Iron seed should start locked.");
await Click("Farm Canvas/Header/ProcessingButton");
if(GameObject.Find("Farm Canvas/Processing Panel")==null)throw new Exception("Workshop did not open.");
await Click("Farm Canvas/Header/LabButton");
if(GameObject.Find("Farm Canvas/Genetic Lab Panel")==null || GameObject.Find("Farm Canvas/Processing Panel")!=null)
    throw new Exception("Tabs did not switch exclusively.");
await Click("Farm Canvas/Header/ShopButton");
await Click("Farm Canvas/Seed Shop Panel/Carrot Shop Row/BuyButton");
await Click("Farm Canvas/Seed Shop Panel/Carrot Shop Row/SellSeedButton");
await Click("Farm Canvas/Garden/SaveButton");
await Click("Farm Canvas/Garden/NewFarmButton");
await Click("Farm Canvas/New Farm Confirmation/Dialog/CancelButton");
if(GameObject.Find("Farm Canvas/New Farm Confirmation")!=null)throw new Exception("New farm cancel failed.");
if(session.Game.Money!=20)throw new Exception("UI trade changed resources unexpectedly.");
var sound=new UnityEditor.SerializedObject(UnityEngine.Object.FindFirstObjectByType<GrowWhatever.FarmAudio>());
var clips=sound.FindProperty("clips");
var audio=new List<object>();
for(int i=0;i<clips.arraySize;i++)
{
    var clip=(AudioClip)clips.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue;
    if(clip!=null && clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
    await Task.Delay(100);
    if(clip==null || clip.length<=0 || clip.loadState!=AudioDataLoadState.Loaded)throw new Exception("Audio clip did not load.");
    audio.Add(new{name=clip.name,seconds=clip.length,channels=clip.channels});
}
UnityEditor.EditorApplication.Step();
await Task.Delay(150);
return new{passed=true,saveStatus=session.SaveStatus,audio=audio.ToArray(),eventSystems=UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length};
