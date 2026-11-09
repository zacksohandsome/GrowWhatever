using TMPro;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class FarmHudView : MonoBehaviour
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private TMP_Text wallet;
        [SerializeField] private TMP_Text selectedSeed;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text plantAllLabel;
        [SerializeField] private TMP_Text harvestAllLabel;
        [SerializeField] private UnityEngine.UI.Button plantAllButton;
        [SerializeField] private UnityEngine.UI.Button harvestAllButton;
        [SerializeField] private UnityEngine.UI.Button supplyButton;
        [SerializeField] private UnityEngine.UI.Button shopButton;
        [SerializeField] private GameObject shopPanel;

        private void OnEnable()
        {
            session.Initialize();
            session.Changed += Refresh;
            plantAllButton.onClick.AddListener(session.PlantAll);
            harvestAllButton.onClick.AddListener(session.HarvestAll);
            supplyButton.onClick.AddListener(session.ClaimSupply);
            shopButton.onClick.AddListener(ToggleShop);
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            plantAllButton.onClick.RemoveListener(session.PlantAll);
            harvestAllButton.onClick.RemoveListener(session.HarvestAll);
            supplyButton.onClick.RemoveListener(session.ClaimSupply);
            shopButton.onClick.RemoveListener(ToggleShop);
        }

        private void ToggleShop() => shopPanel.SetActive(!shopPanel.activeSelf);

        private void Refresh()
        {
            var game = session.Game;
            wallet.text = $"{game.Money}  coins";
            selectedSeed.text = $"{session.Definition(session.SelectedSeed).DisplayName} seeds  /  {game.SeedCount(session.SelectedSeed)} in bag";
            message.text = session.Message;
            int plantable = game.PlantableCount(session.SelectedSeed);
            int harvestable = game.HarvestableCount;
            plantAllLabel.text = $"Plant all ({plantable})";
            harvestAllLabel.text = $"Harvest all ({harvestable})";
            plantAllButton.interactable = plantable > 0;
            harvestAllButton.interactable = harvestable > 0;
            supplyButton.interactable = game.CanClaimSupply;
        }
    }
}
