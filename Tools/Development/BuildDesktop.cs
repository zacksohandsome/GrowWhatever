using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before building.");
bool qa=SessionState.GetBool("GrowWhatever.BuildQA",false);
var session=UnityEngine.Object.FindFirstObjectByType<GrowWhatever.FarmSession>();
var data=new SerializedObject(session);
if(qa)
{
    var context=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("docs/local/validation/qa-context.json"));
    data.FindProperty("saveFileName").stringValue=(string)context["standaloneSlot"];
}
else data.FindProperty("saveFileName").stringValue="farm-save-v1.json";
data.ApplyModifiedPropertiesWithoutUndo();
EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
var symbols=PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';');
if(Array.IndexOf(symbols,"ENABLE_RUNTIME_PIPELINE")>=0 || Array.IndexOf(symbols,"ENABLE_RUNTIME_PIPELINE_DEV")>=0)
    throw new InvalidOperationException("Runtime command server must be excluded from these builds.");
PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
string folder=qa?"Builds/QA":"Builds/GrowWhatever-Windows";
Directory.CreateDirectory(folder);
var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
{
    scenes=new[]{"Assets/Scenes/Farm.unity"},target=BuildTarget.StandaloneWindows64,
    locationPathName=folder+(qa?"/GrowWhatever-QA.exe":"/GrowWhatever.exe"),
    options=qa?BuildOptions.Development:BuildOptions.None
});
var result=new{qa=qa,result=report.summary.result.ToString(),errors=report.summary.totalErrors,
    warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds};
File.WriteAllText("docs/local/validation/"+(qa?"build-qa.json":"build-release.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Desktop build failed; see safe build summary.");
return result;
