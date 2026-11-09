using GrowWhatever.Core;
using UnityEngine;

namespace GrowWhatever
{
    [CreateAssetMenu(menuName = "GrowWhatever/Growable")]
    public sealed class GrowableDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int seedPrice = 2;
        [SerializeField, Min(1)] private float growthSeconds = 30;
        [SerializeField, Min(1)] private int harvestYield = 3;
        [SerializeField, Min(1)] private int producePrice = 1;
        [SerializeField] private Sprite growingSprite;
        [SerializeField] private Sprite matureSprite;

        public string Id => id;
        public string DisplayName => displayName;
        public Sprite GrowingSprite => growingSprite;
        public Sprite MatureSprite => matureSprite;
        public GrowableSpec ToSpec() => new GrowableSpec(id, displayName, seedPrice, growthSeconds, harvestYield, producePrice);
    }
}
