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
        [SerializeField] private TradeQuantityView tradeDialog;
        [SerializeField] private UnityEngine.UI.Button bulkSellButton;
        private void OnEnable()
        {
            session.Initialize(); session.Changed += Refresh;
            queueButton.onClick.AddListener(Queue); sellButton.onClick.AddListener(Sell); Refresh();
            bulkSellButton.onClick.AddListener(BulkSell);
        }
        private void OnDisable()
        {
            session.Changed -= Refresh;
            queueButton.onClick.RemoveListener(Queue); sellButton.onClick.RemoveListener(Sell);
            bulkSellButton.onClick.RemoveListener(BulkSell);
        }
        private void Queue() => session.QueueRecipe(recipeId);
        private void Sell() => session.SellProduce(session.Game.Rules.Recipes.First(x => x.Id == recipeId).OutputId);
        private void BulkSell() => tradeDialog.Open(session.Game.Rules.Recipes.First(x => x.Id == recipeId).OutputId, Core.FarmTradeKind.SellProduce);
        private void Refresh()
        {
            var game = session.Game;
            var recipe = game.Rules.Recipes.First(x => x.Id == recipeId);
            bool unlocked = game.HasPrerequisite(recipe.RequiredResearch);
            title.text = recipe.DisplayName + (unlocked ? "" : " / locked");
            details.text = $"{recipe.InputAmount} {game.ItemName(recipe.InputId)} -> {recipe.OutputAmount} {recipe.OutputName} / {recipe.Seconds:0}s\n" +
                $"Input {game.ProduceCount(recipe.InputId)} / Made {game.ProduceCount(recipe.OutputId)} / Sell {recipe.OutputPrice}c";
            queueButton.interactable = session.Ready && game.CanQueue(recipeId);
            sellButton.interactable = bulkSellButton.interactable = session.Ready && game.QuoteTrade(recipe.OutputId, Core.FarmTradeKind.SellProduce).Maximum > 0;
        }
    }
}
