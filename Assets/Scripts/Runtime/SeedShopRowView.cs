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

        private void OnEnable()
        {
            session.Initialize();
            session.Changed += Refresh;
            selectButton.onClick.AddListener(Select);
            buyButton.onClick.AddListener(Buy);
            sellSeedButton.onClick.AddListener(SellSeed);
            sellProduceButton.onClick.AddListener(SellProduce);
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            selectButton.onClick.RemoveListener(Select);
            buyButton.onClick.RemoveListener(Buy);
            sellSeedButton.onClick.RemoveListener(SellSeed);
            sellProduceButton.onClick.RemoveListener(SellProduce);
        }

        private void Select() => session.SelectSeed(definition.Id);
        private void Buy() => session.BuySeed(definition.Id);
        private void SellSeed() => session.SellSeed(definition.Id);
        private void SellProduce() => session.SellProduce(definition.Id);

        private void Refresh()
        {
            var game = session.Game;
            var spec = game.Definition(definition.Id);
            bool unlocked = game.IsUnlocked(definition.Id);
            title.text = definition.DisplayName + (!unlocked ? " / locked" : session.SelectedSeed == definition.Id ? " / selected" : "");
            inventory.text = $"Seeds {game.SeedCount(definition.Id)} / {spec.ProduceName} {game.ProduceCount(definition.Id)}\n{spec.GrowthSeconds:0}s growth / Yield {spec.Yield + game.YieldBonus} / Sell {spec.ProducePrice}c";
            portrait.sprite = definition.MatureSprite;
            selectButton.interactable = session.Ready && unlocked && session.SelectedSeed != definition.Id;
            buyButton.interactable = session.Ready && unlocked && game.Money >= spec.SeedPrice;
            sellSeedButton.interactable = session.Ready && game.SeedCount(definition.Id) > 0;
            sellProduceButton.interactable = session.Ready && game.ProduceCount(definition.Id) > 0;
        }
    }
}
