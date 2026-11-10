using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

var session=UnityEngine.Object.FindFirstObjectByType<GrowWhatever.FarmSession>();
if(!session.SaveSlot.StartsWith("qa-ui-") || !session.SaveSlot.EndsWith(".corrupt.json"))throw new InvalidOperationException("Use the synthetic corrupt QA slot.");
var path=Path.Combine(Application.persistentDataPath,session.SaveSlot);
string original=File.ReadAllText(path);
if(session.Ready || !session.NeedsRecovery || session.SaveNow())throw new Exception("Corrupt save did not block gameplay and saving.");
UnityEditor.EditorApplication.isPaused=true;UnityEditor.EditorApplication.Step();await Task.Delay(200);
if(File.ReadAllText(path)!=original)throw new Exception("Corrupt save was changed before confirmation.");
var target=GameObject.Find("Farm Canvas/New Farm Confirmation/Dialog/ConfirmButton");
var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,
    position=RectTransformUtility.WorldToScreenPoint(null,target.transform.position)};
var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
if(hits.Count==0 || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=target)throw new Exception("Recovery confirmation not reachable.");
ExecuteEvents.Execute(target,data,ExecuteEvents.pointerClickHandler);
UnityEditor.EditorApplication.Step();await Task.Delay(200);
if(!session.Ready || session.NeedsRecovery || session.Game.Money!=20 || session.Game.SeedCount("carrot")!=5)throw new Exception("Explicit new farm did not restore initial state.");
if(File.ReadAllText(path)==original)throw new Exception("Explicit restart did not replace the synthetic corrupt save.");
return new{passed=true,originalPreservedUntilConfirmation=true,restartRequiredExplicitClick=true};
