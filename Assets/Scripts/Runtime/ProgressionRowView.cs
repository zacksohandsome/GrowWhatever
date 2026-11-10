using System.Linq;
using GrowWhatever.Core;
using TMPro;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class ProgressionRowView : MonoBehaviour
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private string purchaseId;
        [SerializeField] private bool isUpgrade;
        [SerializeField] private TMP_Text title, costLabel, buttonLabel;
        [SerializeField] private UnityEngine.UI.Button purchaseButton;
        private void OnEnable()
        { session.Initialize(); session.Changed += Refresh; purchaseButton.onClick.AddListener(Purchase); Refresh(); }
        private void OnDisable()
        { session.Changed -= Refresh; purchaseButton.onClick.RemoveListener(Purchase); }
        private void Purchase() { if (isUpgrade) session.Upgrade(purchaseId); else session.Research(purchaseId); }
        private void Refresh()
        {
            var game = session.Game;
            ResourceCost cost;
            string name, prerequisite;
            bool done, available;
            if (isUpgrade)
            {
                var spec = game.Rules.Upgrades.First(x => x.Id == purchaseId);
                cost = spec.Cost; name = spec.DisplayName; prerequisite = spec.RequiredResearch;
                done = game.IsUpgraded(purchaseId); available = game.CanUpgrade(purchaseId);
            }
            else
            {
                var spec = game.Rules.Research.First(x => x.Id == purchaseId);
                cost = spec.Cost; name = spec.DisplayName; prerequisite = spec.RequiredResearch;
                done = game.IsResearched(purchaseId); available = game.CanResearch(purchaseId);
            }
            title.text = name + (done ? " / complete" : "");
            costLabel.text = done ? "Permanent. No further payment needed." :
                $"Coins {game.Money}/{cost.Money}\n" + string.Join("   ", cost.Items.Select(x => $"{game.ItemName(x.Key)} {game.ProduceCount(x.Key)}/{x.Value}"));
            buttonLabel.text = done ? "Complete" : !game.HasPrerequisite(prerequisite) ? "Research Iron sprout first" : isUpgrade ? "Buy upgrade" : "Research";
            purchaseButton.interactable = session.Ready && available;
        }
    }
}
