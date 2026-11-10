using System;
using GrowWhatever.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GrowWhatever
{
    public sealed class PlayerSettingsView : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private PlayerSettingsService settings;
        [SerializeField] private FarmSession session;
        [SerializeField] private FarmHudView hud;
        [SerializeField] private TradeQuantityView tradeDialog;
        [SerializeField] private GameObject panel, confirmation;
        [SerializeField] private CanvasGroup content;
        [SerializeField] private CanvasGroup[] backgroundGroups;
        [SerializeField] private UnityEngine.UI.Button openButton, closeButton, modeButton, previousButton, nextButton;
        [SerializeField] private UnityEngine.UI.Button applyButton, defaultsButton, keepButton, revertButton;
        [SerializeField] private UnityEngine.UI.Slider masterSlider, musicSlider, effectsSlider;
        [SerializeField] private TMP_Text modeLabel, resolutionLabel, resolutionHint, masterLabel, musicLabel, effectsLabel;
        [SerializeField] private TMP_Text musicHint, noticeLabel, confirmationText, keepLabel, revertLabel;
        private DisplayChoice draft, lastConfirmed;
        private bool[] previousInteractable, previousRaycasts;
        private GameObject previousSelection;
        private bool resetting, wasPreviewing;
        private int handledFrame = -1;
        public bool IsOpen => panel.activeSelf;

        private void Awake()
        {
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            modeButton.onClick.AddListener(ToggleMode);
            previousButton.onClick.AddListener(PreviousResolution);
            nextButton.onClick.AddListener(NextResolution);
            applyButton.onClick.AddListener(Apply);
            defaultsButton.onClick.AddListener(AskDefaults);
            keepButton.onClick.AddListener(Confirm);
            revertButton.onClick.AddListener(CancelConfirmation);
            masterSlider.onValueChanged.AddListener(VolumeChanged);
            musicSlider.onValueChanged.AddListener(VolumeChanged);
            effectsSlider.onValueChanged.AddListener(VolumeChanged);
        }
        private void OnEnable() { settings.Changed += Refresh; }
        private void OnDisable()
        {
            settings.Changed -= Refresh;
            settings.CancelDisplay(); RestoreBackground();
            panel.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleEscape();
            if (IsOpen && settings.IsPreviewing) UpdateConfirmation();
        }

        public void OnCancel(BaseEventData eventData) { eventData.Use(); HandleEscape(); }
        private void HandleEscape()
        {
            if (handledFrame == Time.frameCount) return;
            handledFrame = Time.frameCount;
            if (tradeDialog.IsOpen || tradeDialog.ClosedFrame == Time.frameCount) return;
            if (hud.HasConfirmation) { hud.CancelConfirmation(); return; }
            if (!session.Ready) return;
            if (!IsOpen) Open();
            else if (settings.IsPreviewing || resetting) CancelConfirmation();
            else Close();
        }

        public void Open()
        {
            if (IsOpen || tradeDialog.IsOpen || hud.HasConfirmation || !session.Ready) return;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            previousInteractable = new bool[backgroundGroups.Length]; previousRaycasts = new bool[backgroundGroups.Length];
            for (int i = 0; i < backgroundGroups.Length; i++)
            {
                previousInteractable[i] = backgroundGroups[i].interactable;
                previousRaycasts[i] = backgroundGroups[i].blocksRaycasts;
                backgroundGroups[i].interactable = false; backgroundGroups[i].blocksRaycasts = false;
            }
            resetting = wasPreviewing = false;
            draft = lastConfirmed = settings.Values.Display;
            panel.transform.SetAsLastSibling(); panel.SetActive(true); Refresh(); Select(closeButton.gameObject);
            session.Play(FarmSound.Click);
        }

        public void Close()
        {
            if (!IsOpen) return;
            if (settings.IsPreviewing || resetting) { CancelConfirmation(); return; }
            panel.SetActive(false); RestoreBackground();
            Select(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            session.Play(FarmSound.Click);
        }
        private void RestoreBackground()
        {
            if (previousInteractable == null) return;
            for (int i = 0; i < backgroundGroups.Length; i++)
                if (backgroundGroups[i] != null)
                { backgroundGroups[i].interactable = previousInteractable[i]; backgroundGroups[i].blocksRaycasts = previousRaycasts[i]; }
            previousInteractable = previousRaycasts = null;
        }

        public void ToggleMode()
        {
            if (!CanEdit) return;
            draft.Borderless = !draft.Borderless;
            if (settings.Resolutions.Length == 0) draft.Borderless = true;
            Refresh();
        }
        public void PreviousResolution() => Cycle(-1);
        public void NextResolution() => Cycle(1);
        private bool CanEdit => IsOpen && !settings.IsPreviewing && !resetting;
        private void Cycle(int direction)
        {
            if (!CanEdit || draft.Borderless) return;
            var sizes = settings.Resolutions;
            if (sizes.Length == 0) return;
            int index = Array.FindIndex(sizes, x => x.Width == draft.Width && x.Height == draft.Height);
            draft = sizes[Math.Max(0, Math.Min(sizes.Length - 1, index + direction))]; Refresh();
        }
        public void Apply()
        {
            if (!CanEdit || draft.Equals(settings.Values.Display)) return;
            settings.BeginPreview(draft); Select(keepButton.gameObject);
        }
        private void VolumeChanged(float unused)
        {
            if (CanEdit) settings.SetVolumes(masterSlider.value, musicSlider.value, effectsSlider.value);
        }
        public void AskDefaults()
        {
            if (!CanEdit) return;
            resetting = true; Refresh(); Select(revertButton.gameObject);
        }
        public void Confirm()
        {
            if (!IsOpen) return;
            if (resetting)
            {
                resetting = false; settings.RestoreDefaults(); Select(keepButton.gameObject);
            }
            else if (settings.IsPreviewing) { settings.KeepDisplay(); if (!settings.IsPreviewing) Select(applyButton.gameObject); }
        }
        public void CancelConfirmation()
        {
            if (!IsOpen) return;
            resetting = false;
            if (settings.IsPreviewing) settings.CancelDisplay();
            Refresh(); Select(closeButton.gameObject);
        }

        private void Refresh()
        {
            if (!IsOpen) return;
            if ((wasPreviewing && !settings.IsPreviewing) || !lastConfirmed.Equals(settings.Values.Display)) draft = settings.Values.Display;
            lastConfirmed = settings.Values.Display; wasPreviewing = settings.IsPreviewing;
            bool modal = resetting || settings.IsPreviewing;
            content.interactable = content.blocksRaycasts = !modal;
            confirmation.SetActive(modal);
            modeLabel.text = draft.Borderless ? "Borderless fullscreen" : "Windowed";
            resolutionLabel.text = draft.Borderless ? "Desktop resolution" : $"{draft.Width} x {draft.Height}";
            resolutionHint.text = draft.Borderless ? "Fullscreen follows your desktop resolution." : "Common 16:9 sizes that fit this display.";
            var sizes = settings.Resolutions;
            int index = Array.FindIndex(sizes, x => x.Width == draft.Width && x.Height == draft.Height);
            previousButton.interactable = !draft.Borderless && index > 0;
            nextButton.interactable = !draft.Borderless && index >= 0 && index < sizes.Length - 1;
            modeButton.interactable = sizes.Length > 0;
            applyButton.interactable = !draft.Equals(settings.Values.Display);
            masterSlider.SetValueWithoutNotify(settings.Values.Master); musicSlider.SetValueWithoutNotify(settings.Values.Music);
            effectsSlider.SetValueWithoutNotify(settings.Values.Effects);
            masterLabel.text = $"{settings.Values.Master * 100:0}%"; musicLabel.text = $"{settings.Values.Music * 100:0}%";
            effectsLabel.text = $"{settings.Values.Effects * 100:0}%";
            musicHint.text = settings.HasMusic ? "" : "No background music yet.";
            noticeLabel.text = settings.Notice;
            UpdateConfirmation();
            if (EventSystem.current != null && !modal && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(confirmation.transform)) Select(closeButton.gameObject);
        }

        private void UpdateConfirmation()
        {
            confirmationText.text = resetting ? "Restore default display and volume settings?\nYour farm progress will be kept." :
                $"Keep these display settings?\nReverting in {settings.SecondsRemaining} seconds.";
            keepLabel.text = resetting ? "Restore defaults" : "Keep changes";
            revertLabel.text = resetting ? "Cancel" : "Revert";
        }
        private static void Select(GameObject target)
        { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target); }
    }
}
