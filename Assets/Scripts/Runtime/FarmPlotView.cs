using TMPro;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class FarmPlotView : MonoBehaviour
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private int plotIndex;
        [SerializeField] private UnityEngine.UI.Button plotButton;
        [SerializeField] private UnityEngine.UI.Button bulkButton;
        [SerializeField] private UnityEngine.UI.Image plantImage;
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text bulkLabel;

        private void OnEnable()
        {
            session.Initialize();
            session.Changed += Refresh;
            plotButton.onClick.AddListener(Interact);
            bulkButton.onClick.AddListener(ToggleBulk);
            Refresh();
        }

        private void OnDisable()
        {
            session.Changed -= Refresh;
            plotButton.onClick.RemoveListener(Interact);
            bulkButton.onClick.RemoveListener(ToggleBulk);
        }

        private void Interact() => session.InteractPlot(plotIndex);
        private void ToggleBulk() => session.ToggleBulk(plotIndex);

        private void Refresh()
        {
            var plot = session.Game.Plots[plotIndex];
            bool mature = session.Game.IsMature(plot);
            plotButton.interactable = bulkButton.interactable = session.Ready;
            plantImage.enabled = !plot.IsEmpty;
            if (!plot.IsEmpty)
            {
                var definition = session.Definition(plot.GrowableId);
                plantImage.sprite = mature ? definition.MatureSprite : definition.GrowingSprite;
                double duration = session.Game.Definition(plot.GrowableId).GrowthSeconds;
                progressFill.fillAmount = (float)(plot.Progress / duration);
                stateLabel.text = mature ? "Ready - harvest" : $"{definition.DisplayName} {System.Math.Ceiling(session.Game.RemainingGrowthSeconds(plot)):0}s";
                progressFill.color = session.Game.NeighborMultiplier(plot) > 1 ? new Color32(165, 193, 247, 255) : new Color32(198, 182, 104, 255);
            }
            else
            {
                progressFill.fillAmount = 0;
                stateLabel.text = "Empty - plant";
            }
            bulkLabel.text = plot.IncludeInBulkHarvest ? "Bulk harvest: ON" : "Bulk harvest: OFF";
        }
    }
}
