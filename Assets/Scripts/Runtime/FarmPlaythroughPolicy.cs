#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using GrowWhatever.Core;

namespace GrowWhatever.QA
{
    // Editor-only QA player. It uses the same resource constraints as a new human player.
    public enum FarmActionKind { None, Harvest, ToggleBulk, Research, Upgrade, Queue, Sell, Buy, Plant }
    public readonly struct FarmPlayerAction
    {
        public FarmActionKind Kind { get; }
        public string Id { get; }
        public int Plot { get; }
        public FarmPlayerAction(FarmActionKind kind, string id = null, int plot = 0) { Kind=kind; Id=id; Plot=plot; }
    }

    public static class FarmPlaythroughPolicy
    {
        public static FarmPlayerAction Decide(FarmGame game)
        {
            if(game.IsGoalComplete) return default;
            for(int i=0;i<game.Plots.Count;i++)
            {
                var plot=game.Plots[i];
                if(plot.GrowableId=="moonflower" && plot.IncludeInBulkHarvest)
                    return new FarmPlayerAction(FarmActionKind.ToggleBulk,plot:i);
                if(game.IsMature(plot) && (plot.GrowableId!="moonflower" || game.UsedNeighborBoost))
                    return new FarmPlayerAction(FarmActionKind.Harvest,plot:i);
            }
            foreach(var research in game.Rules.Research)
                if(game.CanResearch(research.Id)) return new FarmPlayerAction(FarmActionKind.Research,research.Id);
            if(game.IsUnlocked("moonflower") && game.CanUpgrade("growth"))
                return new FarmPlayerAction(FarmActionKind.Upgrade,"growth");

            bool iron=game.IsUnlocked("iron"), moon=game.IsUnlocked("moonflower"), growth=game.IsUpgraded("growth");
            int Needed(string id)=>game.Rules.Research.Where(x=>!game.IsResearched(x.Id)).Sum(x=>x.Cost.Items.TryGetValue(id,out int n)?n:0)+
                game.Rules.Upgrades.Where(x=>x.Id=="growth" && !game.IsUpgraded(x.Id)).Sum(x=>x.Cost.Items.TryGetValue(id,out int n)?n:0);
            int flourTarget=Needed("flour");
            int partsTarget=Needed("parts");
            int flourQueued=game.Jobs.Count(x=>x.RecipeId=="flour");
            int partsQueued=game.Jobs.Count(x=>x.RecipeId=="parts");
            if(game.ProduceCount("flour")+flourQueued<flourTarget && game.CanQueue("flour"))
                return new FarmPlayerAction(FarmActionKind.Queue,"flour");
            if(game.CanQueue("parts")) return new FarmPlayerAction(FarmActionKind.Queue,"parts");

            int wheatReserve=System.Math.Max(0,flourTarget-game.ProduceCount("flour")-flourQueued)*3;
            int ironReserve=System.Math.Max(0,partsTarget-game.ProduceCount("parts")-partsQueued)*2;
            string[] items={"carrot","wheat","iron","moonflower","flour","parts"};
            int[] reserves={Needed("carrot"),wheatReserve,ironReserve,0,flourTarget,partsTarget};
            for(int i=0;i<items.Length;i++)
                if(game.ProduceCount(items[i])>reserves[i]) return new FarmPlayerAction(FarmActionKind.Sell,items[i]);

            int empty=Enumerable.Range(0,9).Where(i=>game.Plots[i].IsEmpty).DefaultIfEmpty(-1).First();
            if(empty<0) return default;
            if(game.Money<2 && game.Growables.All(x=>game.SeedCount(x.Id)==0) && game.Plots.All(x=>x.IsEmpty) && game.Jobs.Count==0)
                foreach(string item in items)
                    if(game.ProduceCount(item)>0) return new FarmPlayerAction(FarmActionKind.Sell,item);
            bool hasMoon=game.Plots.Any(x=>x.GrowableId=="moonflower");
            if(moon && !game.HarvestedMoonEssence && !hasMoon)
                return SeedAction(game,"moonflower",game.Plots[4].IsEmpty?4:empty);
            int InField(string id)=>game.Plots.Count(x=>x.GrowableId==id)*(game.Definition(id).Yield+game.YieldBonus);
            if(!iron && game.ProduceCount("carrot")+InField("carrot")<6)
                return SeedAction(game,"carrot",empty);
            if(game.ProduceCount("wheat")+InField("wheat")<wheatReserve)
                return SeedAction(game,"wheat",empty);
            if(iron && game.ProduceCount("iron")+InField("iron")<ironReserve && (game.SeedCount("iron")>0 || game.Money>=8))
                return SeedAction(game,"iron",empty);
            // Spend the free starting seeds before purchasing income seeds.
            foreach(string id in new[]{"carrot","wheat"})
                if(game.SeedCount(id)>0) return new FarmPlayerAction(FarmActionKind.Plant,id,empty);
            string income=iron && game.Money>=8?"iron":"carrot";
            return SeedAction(game,income,empty);
        }

        private static FarmPlayerAction SeedAction(FarmGame game,string id,int plot)
        {
            if(game.SeedCount(id)>0) return new FarmPlayerAction(FarmActionKind.Plant,id,plot);
            if(game.IsUnlocked(id) && game.Money>=game.Definition(id).SeedPrice) return new FarmPlayerAction(FarmActionKind.Buy,id);
            // If the desired seed is unaffordable, a cheap income plant can fund it.
            if(id!="carrot" && (game.SeedCount("carrot")>0 || game.Money>=2)) return SeedAction(game,"carrot",plot);
            foreach(var seed in game.Growables)
                if(game.IsUnlocked(seed.Id) && game.SeedCount(seed.Id)>0) return new FarmPlayerAction(FarmActionKind.Plant,seed.Id,plot);
            return default;
        }

        public static bool Apply(FarmGame game,FarmPlayerAction action)
        {
            switch(action.Kind)
            {
                case FarmActionKind.Harvest:return game.Harvest(action.Plot);
                case FarmActionKind.ToggleBulk:game.SetBulkHarvest(action.Plot,false);return true;
                case FarmActionKind.Research:return game.Research(action.Id);
                case FarmActionKind.Upgrade:return game.Upgrade(action.Id);
                case FarmActionKind.Queue:return game.QueueRecipe(action.Id);
                case FarmActionKind.Sell:return game.SellProduce(action.Id);
                case FarmActionKind.Buy:return game.BuySeed(action.Id);
                case FarmActionKind.Plant:return game.Plant(action.Plot,action.Id);
                default:return false;
            }
        }
    }
}
#endif
