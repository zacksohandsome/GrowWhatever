using System;

namespace GrowWhatever.Core
{
    public enum FarmTradeKind { BuySeed, SellSeed, SellProduce }

    /// <summary>A current preview, never a reservation of money or inventory.</summary>
    public sealed class FarmTradeQuote
    {
        public string ItemName { get; }
        public int UnitPrice { get; }
        public int Owned { get; }
        public int Maximum { get; }
        public int Money { get; }
        internal FarmTradeQuote(string name, int price, int owned, int maximum, int money)
        { ItemName = name; UnitPrice = price; Owned = owned; Maximum = maximum; Money = money; }
    }

    public sealed partial class FarmGame
    {
        public const int InventoryLimit = 2_000_000_000;

        public FarmTradeQuote QuoteTrade(string id, FarmTradeKind kind)
        {
            if (id == null) return null;
            if (kind == FarmTradeKind.BuySeed || kind == FarmTradeKind.SellSeed)
            {
                if (!content.TryGetValue(id, out var spec)) return null;
                int maximum = kind == FarmTradeKind.BuySeed
                    ? (IsUnlocked(id) ? Math.Min(Money / spec.SeedPrice, Math.Max(0, InventoryLimit - seeds[id])) : 0)
                    : Math.Min(seeds[id], (int.MaxValue - Money) / spec.SeedPrice);
                return new FarmTradeQuote(spec.DisplayName + " seeds", spec.SeedPrice, seeds[id], maximum, Money);
            }
            if (kind != FarmTradeKind.SellProduce || !produce.TryGetValue(id, out int owned)) return null;
            int price = ItemPrice(id);
            return new FarmTradeQuote(ItemName(id), price, owned, Math.Min(owned, (int.MaxValue - Money) / price), Money);
        }

        /// <summary>Revalidate and complete the entire trade, or leave both balances unchanged.</summary>
        public bool Trade(string id, FarmTradeKind kind, int quantity)
        {
            var quote = QuoteTrade(id, kind);
            if (quote == null || quantity <= 0 || quantity > quote.Maximum) return false;
            long total = (long)quantity * quote.UnitPrice;
            var inventory = kind == FarmTradeKind.SellProduce ? produce : seeds;
            bool buying = kind == FarmTradeKind.BuySeed;
            inventory[id] = (int)((long)quote.Owned + (buying ? quantity : -quantity));
            Money = (int)((long)Money + (buying ? -total : total));
            Changed?.Invoke();
            return true;
        }
    }
}
