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
        [SerializeField] private GameObject processingPanel, labPanel, newFarmPanel;
        [SerializeField] private UnityEngine.UI.Button processingButton, labButton, saveButton, newFarmButton, confirmNewButton, cancelNewButton;
        [SerializeField] private TMP_Text goalLabel, saveLabel, recoveryLabel;

        private void OnEnable()
        {
            session.Initialize();
            session.Changed += Refresh;
            plantAllButton.onClick.AddListener(session.PlantAll);
            harvestAllButton.onClick.AddListener(session.HarvestAll);
            supplyButton.onClick.AddListener(session.ClaimSupply);
            shopButton.onClick.AddListener(ToggleShop);
            processingButton.onClick.AddListener(ShowProcessing);
            labButton.onClick.AddListener(ShowLab);
            saveButton.onClick.AddListener(session.SaveManually);
            newFarmButton.onClick.AddListener(AskNewFarm);
            confirmNewButton.onClick.AddListener(ConfirmNewFarm);
            cancelNewButton.onClick.AddListener(CancelNewFarm);
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            plantAllButton.onClick.RemoveListener(session.PlantAll);
            harvestAllButton.onClick.RemoveListener(session.HarvestAll);
            supplyButton.onClick.RemoveListener(session.ClaimSupply);
            shopButton.onClick.RemoveListener(ToggleShop);
            processingButton.onClick.RemoveListener(ShowProcessing);
            labButton.onClick.RemoveListener(ShowLab);
            saveButton.onClick.RemoveListener(session.SaveManually);
            newFarmButton.onClick.RemoveListener(AskNewFarm);
            confirmNewButton.onClick.RemoveListener(ConfirmNewFarm);
            cancelNewButton.onClick.RemoveListener(CancelNewFarm);
        }

        private void ShowPanel(GameObject panel)
        {
            shopPanel.SetActive(panel == shopPanel && !shopPanel.activeSelf);
            processingPanel.SetActive(panel == processingPanel && !processingPanel.activeSelf);
            labPanel.SetActive(panel == labPanel && !labPanel.activeSelf);
            session.Play(FarmSound.Click);
        }
        private void ToggleShop() => ShowPanel(shopPanel);
        private void ShowProcessing() => ShowPanel(processingPanel);
        private void ShowLab() => ShowPanel(labPanel);
        private void AskNewFarm() { newFarmPanel.SetActive(true); session.Play(FarmSound.Click); }
        private void ConfirmNewFarm() { session.StartNewFarm(); newFarmPanel.SetActive(false); }
        private void CancelNewFarm() { if (!session.NeedsRecovery) newFarmPanel.SetActive(false); session.Play(FarmSound.Click); }

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
            plantAllButton.interactable = session.Ready && plantable > 0;
            harvestAllButton.interactable = session.Ready && harvestable > 0;
            supplyButton.interactable = session.Ready && game.CanClaimSupply;
            saveButton.interactable = session.Ready;
            goalLabel.text = session.CurrentGoal;
            goalLabel.color = game.IsGoalComplete ? new Color32(242, 209, 123, 255) : new Color32(179, 190, 162, 255);
            saveLabel.text = session.SaveStatus;
            cancelNewButton.interactable = !session.NeedsRecovery;
            recoveryLabel.text = session.NeedsRecovery ? "Save could not be loaded. It has not been overwritten.\nStart a new farm to replace it, or quit to keep the original." :
                "Start a new farm? Your current local save will be replaced.\nThis cannot be undone.";
            if (session.NeedsRecovery) newFarmPanel.SetActive(true);
        }
    }
}
