using TMPro;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class SeedShopRowView : MonoBehaviour
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private GrowableDefinition definition;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text inventory;
        [SerializeField] private UnityEngine.UI.Image portrait;
        [SerializeField] private UnityEngine.UI.Button selectButton;
        [SerializeField] private UnityEngine.UI.Button buyButton;
        [SerializeField] private UnityEngine.UI.Button sellSeedButton;
        [SerializeField] private UnityEngine.UI.Button sellProduceButton;
        [SerializeField] private TradeQuantityView tradeDialog;
        [SerializeField] private UnityEngine.UI.Button bulkBuyButton, bulkSellSeedButton, bulkSellProduceButton;

        private void OnEnable()
        {
            session.Initialize();
            session.Changed += Refresh;
            selectButton.onClick.AddListener(Select);
            buyButton.onClick.AddListener(Buy);
            sellSeedButton.onClick.AddListener(SellSeed);
            sellProduceButton.onClick.AddListener(SellProduce);
            bulkBuyButton.onClick.AddListener(BulkBuy);
            bulkSellSeedButton.onClick.AddListener(BulkSellSeed);
            bulkSellProduceButton.onClick.AddListener(BulkSellProduce);
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            selectButton.onClick.RemoveListener(Select);
            buyButton.onClick.RemoveListener(Buy);
            sellSeedButton.onClick.RemoveListener(SellSeed);
            sellProduceButton.onClick.RemoveListener(SellProduce);
            bulkBuyButton.onClick.RemoveListener(BulkBuy);
            bulkSellSeedButton.onClick.RemoveListener(BulkSellSeed);
            bulkSellProduceButton.onClick.RemoveListener(BulkSellProduce);
        }

        private void Select() => session.SelectSeed(definition.Id);
        private void Buy() => session.BuySeed(definition.Id);
        private void SellSeed() => session.SellSeed(definition.Id);
        private void SellProduce() => session.SellProduce(definition.Id);
        private void BulkBuy() => tradeDialog.Open(definition.Id, Core.FarmTradeKind.BuySeed);
        private void BulkSellSeed() => tradeDialog.Open(definition.Id, Core.FarmTradeKind.SellSeed);
        private void BulkSellProduce() => tradeDialog.Open(definition.Id, Core.FarmTradeKind.SellProduce);

        private void Refresh()
        {
            var game = session.Game;
            var spec = game.Definition(definition.Id);
            bool unlocked = game.IsUnlocked(definition.Id);
            title.text = definition.DisplayName + (!unlocked ? " / locked" : session.SelectedSeed == definition.Id ? " / selected" : "");
            inventory.text = $"Seeds {game.SeedCount(definition.Id)} / {spec.ProduceName} {game.ProduceCount(definition.Id)}\n{spec.GrowthSeconds:0}s growth / Yield {spec.Yield + game.YieldBonus} / Sell {spec.ProducePrice}c";
            portrait.sprite = definition.MatureSprite;
            selectButton.interactable = session.Ready && unlocked && session.SelectedSeed != definition.Id;
            buyButton.interactable = bulkBuyButton.interactable = session.Ready && game.QuoteTrade(definition.Id, Core.FarmTradeKind.BuySeed).Maximum > 0;
            sellSeedButton.interactable = bulkSellSeedButton.interactable = session.Ready && game.QuoteTrade(definition.Id, Core.FarmTradeKind.SellSeed).Maximum > 0;
            sellProduceButton.interactable = bulkSellProduceButton.interactable = session.Ready && game.QuoteTrade(definition.Id, Core.FarmTradeKind.SellProduce).Maximum > 0;
        }
    }
}
