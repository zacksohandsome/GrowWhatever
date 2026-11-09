using System;

namespace GrowWhatever.Core
{
    /// <summary>Immutable rules copied from authored settings; contains no save or view state.</summary>
    public sealed class GrowableSpec
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int SeedPrice { get; }
        public double GrowthSeconds { get; }
        public int Yield { get; }
        public int ProducePrice { get; }

        public GrowableSpec(string id, string displayName, int seedPrice, double growthSeconds,
            int yield, int producePrice)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable content ID is required.", nameof(id));
            if (seedPrice <= 0 || growthSeconds <= 0 || double.IsNaN(growthSeconds) ||
                double.IsInfinity(growthSeconds) || yield <= 0 || producePrice <= 0)
                throw new ArgumentOutOfRangeException(nameof(seedPrice), "Content values must be positive and finite.");
            Id = id;
            DisplayName = displayName;
            SeedPrice = seedPrice;
            GrowthSeconds = growthSeconds;
            Yield = yield;
            ProducePrice = producePrice;
        }
    }
}
