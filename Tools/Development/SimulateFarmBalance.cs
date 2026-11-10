using System.Linq;
using GrowWhatever;
using GrowWhatever.Core;
using GrowWhatever.QA;
using UnityEditor;

var config=AssetDatabase.LoadAssetAtPath<FarmConfiguration>("Assets/Data/FarmConfiguration.asset");
var definitions=AssetDatabase.FindAssets("t:GrowableDefinition",new[]{"Assets/Data/Growables"})
    .Select(x=>AssetDatabase.LoadAssetAtPath<GrowableDefinition>(AssetDatabase.GUIDToAssetPath(x)).ToSpec()).ToArray();
var game=config.CreateGame(definitions);
int first=0,iron=0,moon=0,actions=0,time=0;
for(time=0;time<7200 && !game.IsGoalComplete;time++)
{
    for(int i=0;i<12;i++)
    {
        var action=FarmPlaythroughPolicy.Decide(game);
        if(action.Kind==FarmActionKind.None)break;
        if(!FarmPlaythroughPolicy.Apply(game,action)) throw new System.Exception("Policy action failed.");
        actions++;
        if(action.Kind==FarmActionKind.Harvest && first==0) first=time;
    }
    if(game.IsUnlocked("iron") && iron==0)iron=time;
    if(game.IsUnlocked("moonflower") && moon==0)moon=time;
    game.Advance(1);
}
return new { complete=game.IsGoalComplete,seconds=time,firstHarvest=first,ironUnlocked=iron,moonUnlocked=moon,actions=actions,money=game.Money,
    seeds=game.Capture(System.DateTime.UtcNow.Ticks).Seeds,items=game.Capture(System.DateTime.UtcNow.Ticks).Items,
    planted=game.Plots.Count(x=>!x.IsEmpty),jobs=game.Jobs.Count };
