// Accelerated visual fixture only. This is separate from the real-time Windows trial.
using System;
using GrowWhatever.QA;
using UnityEngine;

var session=UnityEngine.Object.FindFirstObjectByType<GrowWhatever.FarmSession>();
if(!session.SaveSlot.StartsWith("qa-ui-"))throw new InvalidOperationException("Use the isolated UI save slot.");
var game=session.Game;
int simulated=0;
while(!game.IsGoalComplete && simulated<7200)
{
    for(int i=0;i<12;i++)
    {
        var action=FarmPlaythroughPolicy.Decide(game);
        if(action.Kind==FarmActionKind.None)break;
        if(!FarmPlaythroughPolicy.Apply(game,action))throw new InvalidOperationException("Visual fixture progression failed.");
    }
    game.Advance(1); simulated++;
}
if(!game.IsGoalComplete)throw new InvalidOperationException("Visual fixture did not reach the goal.");
game.Advance(200); game.HarvestAll();
for(int i=0;i<9;i++) if(game.IsMature(game.Plots[i]))game.Harvest(i);
foreach(string item in new[]{"carrot","wheat","iron","moonflower","flour","parts"})while(game.SellProduce(item)){}
string[] planting={"carrot","wheat","iron","carrot","moonflower","iron","wheat","carrot","iron"};
for(int i=0;i<9;i++)
{
    if(game.SeedCount(planting[i])==0 && !game.BuySeed(planting[i]))throw new InvalidOperationException("Visual fixture budget insufficient.");
    if(!game.Plant(i,planting[i]))throw new InvalidOperationException("Visual fixture planting failed.");
}
game.SetBulkHarvest(4,false); game.Advance(10);
var canvas=GameObject.Find("Farm Canvas").transform;
canvas.Find("Processing Panel").gameObject.SetActive(false);canvas.Find("Genetic Lab Panel").gameObject.SetActive(false);canvas.Find("Seed Shop Panel").gameObject.SetActive(true);
UnityEditor.EditorApplication.isPaused=true;UnityEditor.EditorApplication.Step();await System.Threading.Tasks.Task.Delay(200);
return new{fixture=true,acceleratedSeconds=simulated,goalComplete=game.IsGoalComplete,usedNeighborBoost=game.UsedNeighborBoost,moonflowerKept=!game.Plots[4].IncludeInBulkHarvest};
