using System.Linq;
using TMPro;
using UnityEngine;

namespace GrowWhatever
{
    public sealed class RecipeRowView : MonoBehaviour
    {
        [SerializeField] private FarmSession session;
        [SerializeField] private string recipeId;
        [SerializeField] private TMP_Text title, details;
        [SerializeField] private UnityEngine.UI.Button queueButton, sellButton;
        private void OnEnable()
        {
            session.Initialize(); session.Changed += Refresh;
            queueButton.onClick.AddListener(Queue); sellButton.onClick.AddListener(Sell); Refresh();
        }
        private void OnDisable()
        {
            session.Changed -= Refresh;
            queueButton.onClick.RemoveListener(Queue); sellButton.onClick.RemoveListener(Sell);
        }
        private void Queue() => session.QueueRecipe(recipeId);
        private void Sell() => session.SellProduce(session.Game.Rules.Recipes.First(x => x.Id == recipeId).OutputId);
        private void Refresh()
        {
            var game = session.Game;
            var recipe = game.Rules.Recipes.First(x => x.Id == recipeId);
            bool unlocked = game.HasPrerequisite(recipe.RequiredResearch);
            title.text = recipe.DisplayName + (unlocked ? "" : " / locked");
            details.text = $"{recipe.InputAmount} {game.ItemName(recipe.InputId)} -> {recipe.OutputAmount} {recipe.OutputName} / {recipe.Seconds:0}s\n" +
                $"Input {game.ProduceCount(recipe.InputId)} / Made {game.ProduceCount(recipe.OutputId)} / Sell {recipe.OutputPrice}c";
            queueButton.interactable = session.Ready && game.CanQueue(recipeId);
            sellButton.interactable = session.Ready && game.ProduceCount(recipe.OutputId) > 0;
        }
    }
}
