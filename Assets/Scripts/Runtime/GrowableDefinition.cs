using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    [CreateAssetMenu(menuName = "GrowWhatever/Growable")]
    public sealed class GrowableDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string produceName;
        [SerializeField, Min(1)] private int seedPrice = 2;
        [SerializeField, Min(1)] private float growthSeconds = 30;
        [SerializeField, Min(1)] private int harvestYield = 3;
        [SerializeField, Min(1)] private int producePrice = 1;
        [SerializeField] private Sprite growingSprite;
        [SerializeField] private Sprite matureSprite;
        [SerializeField] private bool initiallyUnlocked = true;
        [SerializeField] private bool grantsNeighborBoost;
        [SerializeField, Min(1)] private float neighborMultiplier = 1.25f;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite GrowingSprite => growingSprite;
        public Sprite MatureSprite => matureSprite;
        public GrowableSpec ToSpec() => new GrowableSpec(id, displayName, seedPrice, growthSeconds, harvestYield, producePrice,
            initiallyUnlocked, grantsNeighborBoost ? new MoonflowerGrowthEffect(neighborMultiplier) : null, produceName);
    }
}
