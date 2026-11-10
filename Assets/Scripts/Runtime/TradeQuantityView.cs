using System;
using GrowWhatever.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GrowWhatever
{
    /// <summary>Shared modal for all seed, harvest and processed-item trades.</summary>
    public sealed class TradeQuantityView : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private TMP_Text title, details, feedback;
        [SerializeField] private TMP_InputField quantityInput;
        [SerializeField] private UnityEngine.UI.Button minimumButton, minusTenButton, minusOneButton;
        [SerializeField] private UnityEngine.UI.Button plusOneButton, plusTenButton, maximumButton;
        [SerializeField] private UnityEngine.UI.Button confirmButton, cancelButton, outsideButton;
        [SerializeField] private CanvasGroup[] backgroundGroups;
        private bool[] backgroundInteractable;
        private string itemId;
        private FarmTradeKind kind;
        private int quantity, maximum;
        private bool submitting;
        private GameObject previousSelection;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            minimumButton.onClick.AddListener(() => SetQuantity(0));
            minusTenButton.onClick.AddListener(() => Adjust(-10));
            minusOneButton.onClick.AddListener(() => Adjust(-1));
            plusOneButton.onClick.AddListener(() => Adjust(1));
            plusTenButton.onClick.AddListener(() => Adjust(10));
            maximumButton.onClick.AddListener(() => SetQuantity(maximum));
            quantityInput.onValueChanged.AddListener(PreviewInput);
            quantityInput.onEndEdit.AddListener(CommitInput);
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Close);
            outsideButton.onClick.AddListener(Close);
        }

        private void OnEnable() => session.Changed += Refresh;
        private void OnDisable()
        {
            session.Changed -= Refresh;
            RestoreBackground();
        }

        private void LateUpdate()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        public void OnCancel(BaseEventData eventData) { eventData.Use(); Close(); }

        public void Open(string id, FarmTradeKind tradeKind)
        {
            if (!session.Ready || IsOpen) return;
            itemId = id;
            kind = tradeKind;
            quantity = 1;
            submitting = false;
            feedback.text = "";
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            backgroundInteractable = new bool[backgroundGroups.Length];
            for (int i = 0; i < backgroundGroups.Length; i++)
            {
                backgroundInteractable[i] = backgroundGroups[i].interactable;
                backgroundGroups[i].interactable = false;
            }
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            Refresh();
            quantityInput.SetTextWithoutNotify(quantity.ToString());
            session.Play(FarmSound.Click);
        }

        public void Close()
        {
            if (!IsOpen || submitting) return;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            gameObject.SetActive(false);
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
        }

        private void RestoreBackground()
        {
            if (backgroundInteractable == null) return;
            for (int i = 0; i < backgroundGroups.Length; i++)
                if (backgroundGroups[i] != null) backgroundGroups[i].interactable = backgroundInteractable[i];
            backgroundInteractable = null;
        }

        // Saturate arbitrarily large positive input without overflowing an integer parser.
        public static int ParseQuantity(string text, int limit)
        {
            if (string.IsNullOrWhiteSpace(text) || text[0] == '-') return 0;
            long value = 0;
            foreach (char digit in text)
            {
                if (digit < '0' || digit > '9') return 0;
                value = Math.Min((long)Math.Max(0, limit), value * 10 + digit - '0');
            }
            return (int)value;
        }

        private void PreviewInput(string text)
        {
            quantity = ParseQuantity(text, maximum);
            feedback.text = "";
            Refresh();
        }

        private void CommitInput(string text) => SetQuantity(ParseQuantity(text, maximum));
        private void Adjust(int delta) => SetQuantity((int)Math.Max(0L, Math.Min((long)maximum, (long)quantity + delta)));
        private void SetQuantity(int value)
        {
            quantity = Math.Max(0, Math.Min(maximum, value));
            feedback.text = "";
            quantityInput.SetTextWithoutNotify(quantity.ToString());
            Refresh();
        }

        private void Refresh()
        {
            var quote = session.Game.QuoteTrade(itemId, kind);
            maximum = session.Ready && quote != null ? quote.Maximum : 0;
            int clamped = Math.Min(quantity, maximum);
            if (clamped != quantity || !quantityInput.isFocused)
                quantityInput.SetTextWithoutNotify(clamped.ToString());
            quantity = clamped;
            bool buying = kind == FarmTradeKind.BuySeed;
            title.text = (buying ? "BUY / " : "SELL / ") + (quote?.ItemName ?? "Unavailable item");
            long total = (long)quantity * (quote?.UnitPrice ?? 0);
            long ownedAfter = (quote?.Owned ?? 0L) + (buying ? quantity : -quantity);
            long moneyAfter = (quote?.Money ?? 0L) + (buying ? -total : total);
            details.text = $"Unit price: {quote?.UnitPrice ?? 0:N0} coins   /   Maximum: {maximum:N0}\n" +
                $"{(buying ? "Total cost" : "Total income")}: {total:N0} coins\n" +
                $"Inventory: {quote?.Owned ?? 0:N0} -> {ownedAfter:N0}\n" +
                $"Coins: {quote?.Money ?? 0:N0} -> {moneyAfter:N0}";
            confirmButton.interactable = !submitting && session.Ready && quantity > 0;
            minimumButton.interactable = minusTenButton.interactable = minusOneButton.interactable = !submitting && quantity > 0;
            plusOneButton.interactable = plusTenButton.interactable = maximumButton.interactable = !submitting && quantity < maximum;
            quantityInput.interactable = cancelButton.interactable = outsideButton.interactable = !submitting;
        }

        private void Confirm()
        {
            if (submitting || !IsOpen || quantity <= 0) return;
            // Capture what was displayed BEFORE settling time can refresh and clamp the view.
            int requested = quantity;
            submitting = true;
            Refresh();
            bool success;
            try { success = session.Trade(itemId, kind, requested); }
            finally { submitting = false; }
            if (success) Close();
            else
            {
                Refresh();
                feedback.text = "Resources changed or quantity unavailable.\nReview the updated quantity and confirm again.";
            }
        }
    }
}
