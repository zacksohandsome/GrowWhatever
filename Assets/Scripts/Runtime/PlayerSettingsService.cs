using System;
using System.Collections;
using System.IO;
using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerSettingsService : MonoBehaviour
    {
        [SerializeField] private string settingsFileName = "player-settings-v1.json";
        [SerializeField] private AudioSource musicSource;
        private PlayerPreferencesStore store;
        private DisplayPreview preview;
        private int desktopWidth, desktopHeight;
        private double nextMonitorCheck, previewStarted;
        public PlayerPreferences Values { get; private set; }
        public bool IsPreviewing => preview != null && preview.IsActive;
        public int SecondsRemaining => IsPreviewing ? Math.Max(0, (int)Math.Ceiling(preview.Deadline - Time.realtimeSinceStartupAsDouble)) : 0;
        public string Notice { get; private set; } = "";
        public bool HasMusic => musicSource != null && musicSource.clip != null;
        public float EffectsGain => Values.Master * Values.Effects;
        public DisplayChoice[] Resolutions => PlayerPreferences.AvailableResolutions(desktopWidth, desktopHeight);
        public event Action Changed;

        private void Awake()
        {
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            if (PlayerSettingsStandaloneCheck.Requested) settingsFileName = "qa-player-settings-validation.json";
#endif
            if (string.IsNullOrWhiteSpace(settingsFileName) || Path.GetFileName(settingsFileName) != settingsFileName)
                throw new InvalidOperationException("Settings file name must be a local file name.");
            store = new PlayerPreferencesStore(Path.Combine(Application.persistentDataPath, settingsFileName));
            Values = store.Read(out bool invalid);
            ReadDesktop();
            bool adjusted = Values.Normalize(desktopWidth, desktopHeight);
            Notice = invalid ? "Settings could not be loaded. Defaults are in use." : adjusted ? "Settings adjusted to fit this display." : "";
            preview = new DisplayPreview(Values.Display);
            ApplyDisplay(Values.Display); ApplyAudio();
        }

        private IEnumerator Start()
        {
            // Windows can finish restoring the previous window size after the fullscreen mode changes.
            // Retry across startup frames so that stale OS state cannot win over confirmed preferences.
            yield return null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (IsPreviewing) yield break;
                ApplyDisplay(Values.Display);
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }

        private void ReadDesktop()
        {
            var display = Screen.mainWindowDisplayInfo;
            desktopWidth = Math.Max(1, display.width); desktopHeight = Math.Max(1, display.height);
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (preview.Expire(now)) { ApplyDisplay(preview.Confirmed); Changed?.Invoke(); }
            if (now < nextMonitorCheck) return;
            nextMonitorCheck = now + 1;
            int oldWidth = desktopWidth, oldHeight = desktopHeight;
            ReadDesktop();
            if (oldWidth == desktopWidth && oldHeight == desktopHeight) return;
            preview.Cancel();
            Values.Normalize(desktopWidth, desktopHeight);
            preview = new DisplayPreview(Values.Display);
            ApplyDisplay(Values.Display);
            Notice = "Display changed. Settings adjusted to fit.";
            Save(); Changed?.Invoke();
        }

        public bool BeginPreview(DisplayChoice choice)
        {
            if (IsPreviewing) return false;
            choice = PlayerPreferences.Fit(choice, desktopWidth, desktopHeight);
            previewStarted = Time.realtimeSinceStartupAsDouble;
            preview.Begin(choice, previewStarted); ApplyDisplay(choice); Changed?.Invoke();
            return true;
        }

        public void KeepDisplay()
        {
            if (!IsPreviewing) return;
#if !UNITY_EDITOR
            // Resolution changes complete at the end of a frame. Never save a rejected OS request.
            if (Time.realtimeSinceStartupAsDouble - previewStarted < 0.5) return;
            var pending = preview.Pending;
            bool actualBorderless = Screen.fullScreenMode == FullScreenMode.FullScreenWindow;
            if (actualBorderless != pending.Borderless || (!pending.Borderless && (Screen.width != pending.Width || Screen.height != pending.Height)))
            { CancelDisplay(); Notice = "Display change was unavailable. Previous settings restored."; Changed?.Invoke(); return; }
#endif
            if (preview.Keep(Time.realtimeSinceStartupAsDouble)) { Values.Display = preview.Confirmed; Save(); }
            else ApplyDisplay(preview.Confirmed);
            Changed?.Invoke();
        }

        public void CancelDisplay()
        {
            if (!IsPreviewing) return;
            preview.Cancel(); ApplyDisplay(preview.Confirmed); Changed?.Invoke();
        }

        public void SetVolumes(float master, float music, float effects)
        {
            Values.Master = PlayerPreferences.ClampVolume(master, 1);
            Values.Music = PlayerPreferences.ClampVolume(music, 0.7f);
            Values.Effects = PlayerPreferences.ClampVolume(effects, 0.7f);
            ApplyAudio(); Save(); Changed?.Invoke();
        }

        public void RestoreDefaults()
        {
            if (IsPreviewing) return;
            var defaults = new PlayerPreferences();
            SetVolumes(defaults.Master, defaults.Music, defaults.Effects);
            BeginPreview(defaults.Display);
        }

        private void ApplyAudio() { if (musicSource != null) musicSource.volume = Values.Master * Values.Music; }
        private void Save()
        {
            if (!store.Write(Values)) Notice = "Settings could not be saved. Check local storage access.";
        }
        private void ApplyDisplay(DisplayChoice choice)
        {
#if !UNITY_EDITOR
            Screen.SetResolution(choice.Borderless ? desktopWidth : choice.Width, choice.Borderless ? desktopHeight : choice.Height,
                choice.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
    }
}
