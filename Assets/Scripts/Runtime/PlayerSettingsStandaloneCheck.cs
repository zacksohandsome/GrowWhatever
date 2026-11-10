#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    /// <summary>Opt-in local Windows QA; excluded from release builds and uses separate synthetic saves.</summary>
    public sealed class PlayerSettingsStandaloneCheck : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-settings-qa") >= 0;
        private static string Phase
        {
            get
            {
                var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-settings-qa");
                return index >= 0 && index + 1 < args.Length ? args[index+1] : "fresh";
            }
        }
        private readonly List<string> passed = new List<string>();
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Prepare()
        {
            if (!Requested) return;
            string path = Path.Combine(Application.persistentDataPath,"qa-player-settings-validation.json");
            Directory.CreateDirectory(Application.persistentDataPath);
            if (Phase == "fresh") File.WriteAllText(path,JsonUtility.ToJson(new PlayerPreferences()));
            if (Phase == "damaged") File.WriteAllText(path,"synthetic damaged preferences");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartQA()
        { if (Requested) new GameObject("Settings standalone QA").AddComponent<PlayerSettingsStandaloneCheck>(); }

        private IEnumerator Start()
        {
            output = Path.GetFullPath(Path.Combine(Application.dataPath,"../settings-qa-" +
                (Phase == "fresh" || Phase == "reload" || Phase == "interrupted" || Phase == "damaged" ? Phase : "unknown") + ".json"));
            // Catch iterator failures without logging arbitrary local paths or exception details.
            var run = Verify();
            while (true)
            {
                bool next; object current;
                try { next = run.MoveNext(); current = next ? run.Current : null; }
                catch (Exception) { Finish(false); yield break; }
                if (!next) break;
                yield return current;
            }
            Finish(true);
        }

        private IEnumerator Verify()
        {
            yield return new WaitForSecondsRealtime(1);
            var service = FindFirstObjectByType<PlayerSettingsService>();
            var view = FindFirstObjectByType<PlayerSettingsView>();
            var session = FindFirstObjectByType<FarmSession>();
            Check(session.SaveSlot == "qa-player-settings-farm-validation.json","isolated farm slot");
            if (Phase == "damaged")
            {
                Check(service.Notice.Contains("could not be loaded") && service.Values.Master == 1,"damaged settings fallback");
                Check(session.Ready,"farm loads independently"); yield break;
            }
            if (Phase == "reload" || Phase == "interrupted")
            {
                Check(service.Values.Master == 0.5f && service.Values.Effects == 0.4f,"audio survives restart");
                Check(!service.Values.Display.Borderless && Screen.width == 1280 && Screen.height == 720,"confirmed window survives restart");
                if (Phase == "interrupted")
                {
                    service.BeginPreview(new DisplayChoice(1280,720,true));
                    yield return new WaitForSecondsRealtime(1);
                    Check(service.IsPreviewing && Screen.fullScreenMode == FullScreenMode.FullScreenWindow,"quit during unconfirmed preview");
                }
                yield break;
            }
            Check(Phase == "fresh","known QA phase");
            Check(Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == 1280 && Screen.height == 720,"default window dimensions");
            view.Open(); yield return null;
            var master = GameObject.Find("Farm Canvas/Player Settings/Dialog/MasterSlider").GetComponent<UnityEngine.UI.Slider>();
            var effects = GameObject.Find("Farm Canvas/Player Settings/Dialog/EffectsSlider").GetComponent<UnityEngine.UI.Slider>();
            master.value=0.5f; effects.value=0;
            Check(FindFirstObjectByType<FarmAudio>().GetComponent<AudioSource>().volume == 0,"effects mute");
            effects.value=0.4f;
            Check(Math.Abs(FindFirstObjectByType<FarmAudio>().GetComponent<AudioSource>().volume-0.2f)<0.001,"combined audio gain");
            session.PlantAll(); double progress=session.Game.Plots[0].Progress;
            yield return new WaitForSecondsRealtime(1);
            Check(session.Game.Plots[0].Progress>progress,"growth continues in settings");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../settings-1280x720.png")));
            yield return new WaitForEndOfFrame();
            view.Close();
            session.StartNewFarm();
            Check(service.Values.Master==0.5f,"new farm preserves settings");
            service.BeginPreview(new DisplayChoice(1280,720,true));
            yield return new WaitForSecondsRealtime(1);
            Check(Screen.fullScreenMode==FullScreenMode.FullScreenWindow,"borderless mode applied");
            var desktop=Screen.mainWindowDisplayInfo;
            Check(Screen.width==desktop.width && Screen.height==desktop.height,"borderless follows desktop");
            service.CancelDisplay(); yield return new WaitForSecondsRealtime(1);
            Check(Screen.fullScreenMode==FullScreenMode.Windowed && Screen.width==1280,"cancel restores window");
            service.BeginPreview(new DisplayChoice(1280,720,true));
            yield return new WaitForSecondsRealtime(1); service.KeepDisplay();
            Check(!service.IsPreviewing&&service.Values.Display.Borderless,"keep confirms display");
            service.BeginPreview(new DisplayChoice(1280,720));
            yield return new WaitForSecondsRealtime(1); service.KeepDisplay();
            Check(!service.IsPreviewing&&!service.Values.Display.Borderless,"return to window confirmed");
            foreach (var size in service.Resolutions)
            {
                service.BeginPreview(size); yield return new WaitForSecondsRealtime(0.7f);
                Check(Screen.width==size.Width&&Screen.height==size.Height,"window size " + size.Width + "x" + size.Height);
                service.CancelDisplay(); yield return new WaitForSecondsRealtime(0.3f);
            }
            service.BeginPreview(new DisplayChoice(1280,720,true));
            yield return new WaitForSecondsRealtime(16);
            Check(!service.IsPreviewing&&Screen.fullScreenMode==FullScreenMode.Windowed&&Screen.width==1280,"real timeout restores display");
            service.SetVolumes(0,0.7f,0.4f);
            Check(FindFirstObjectByType<FarmAudio>().GetComponent<AudioSource>().volume==0,"master mute");
            service.SetVolumes(0.5f,0.7f,0.4f);
        }

        private void Check(bool condition,string name)
        {
            if (!condition) { passed.Add("FAILED: " + name); throw new InvalidOperationException(); }
            passed.Add(name);
        }
        [Serializable] private sealed class Report
        { public bool success; public string[] checks; public int width, height; public string mode; public bool savedBorderless; }
        private void Finish(bool success)
        {
            File.WriteAllText(output,JsonUtility.ToJson(new Report { success=success, checks=passed.ToArray(), width=Screen.width,
                height=Screen.height, mode=Screen.fullScreenMode.ToString(), savedBorderless=FindFirstObjectByType<PlayerSettingsService>().Values.Display.Borderless },true));
            Application.Quit(success ? 0 : 1);
        }
    }
}
#endif
