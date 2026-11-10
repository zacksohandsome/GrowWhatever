using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/Scenes/Farm.unity" || scene.isDirty) throw new InvalidOperationException("Saved Farm scene required.");
var symbols=PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Split(';');
if(Array.IndexOf(symbols,"ENABLE_RUNTIME_PIPELINE")>=0 || Array.IndexOf(symbols,"ENABLE_RUNTIME_PIPELINE_DEV")>=0)
    throw new InvalidOperationException("Runtime command server must remain excluded.");
Directory.CreateDirectory("Builds/Settings-QA");
var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
    scenes=new[]{scene.path}, target=BuildTarget.StandaloneWindows64,
    locationPathName="Builds/Settings-QA/GrowWhatever-SettingsQA.exe", options=BuildOptions.Development });
var result=new { result=report.summary.result.ToString(), errors=report.summary.totalErrors,
    warnings=report.summary.totalWarnings, seconds=report.summary.totalTime.TotalSeconds };
Directory.CreateDirectory("docs/local/validation");
File.WriteAllText("docs/local/validation/settings-build-qa.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
if(report.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("Settings QA build failed.");
return result;
