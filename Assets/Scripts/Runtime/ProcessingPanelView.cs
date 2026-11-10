using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class ProcessingPanelView : MonoBehaviour
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private TMP_Text queueLabel;
        private void OnEnable() { session.Initialize(); session.Changed += Refresh; Refresh(); }
        private void OnDisable() { session.Changed -= Refresh; }
        private void Refresh()
        {
            var game = session.Game;
            queueLabel.text = $"WORK QUEUE / {game.Jobs.Count} of {Core.FarmGame.QueueCapacity}\n\n" +
                (game.Jobs.Count == 0 ? "No work queued.\nMaterials are paid when you order." :
                    string.Join("\n", game.Jobs.Select((x, i) => $"{(i == 0 ? "Working" : "Waiting " + i)}: " +
                        $"{game.Rules.Recipes.First(r => r.Id == x.RecipeId).OutputName} / {Math.Ceiling(x.RemainingSeconds):0}s"))) +
                "\n\nCompleted goods go straight into your inventory.";
        }
    }
}
