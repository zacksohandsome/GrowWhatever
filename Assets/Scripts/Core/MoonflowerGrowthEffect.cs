using System;
using System.Collections.Generic;

namespace GrowWhatever.Core
{
    public interface IGrowthEffect
    {
        double Multiplier(FarmPlot target, IReadOnlyList<FarmPlot> plots, Func<string, GrowableSpec> definition);
    }

    public sealed class MoonflowerGrowthEffect : IGrowthEffect
    {
        private readonly double multiplier;
        public MoonflowerGrowthEffect(double multiplier)
        {
            if (multiplier < 1 || double.IsNaN(multiplier) || double.IsInfinity(multiplier))
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            this.multiplier = multiplier;
        }

        public double Multiplier(FarmPlot target, IReadOnlyList<FarmPlot> plots, Func<string, GrowableSpec> definition)
        {
            if (target.IsEmpty || definition(target.GrowableId).GrowthEffect != null) return 1;
            foreach (var source in plots)
                if (!source.IsEmpty && definition(source.GrowableId).GrowthEffect is MoonflowerGrowthEffect &&
                    Math.Abs(target.Row - source.Row) + Math.Abs(target.Column - source.Column) == 1)
                    return multiplier;
            return 1;
        }
    }
}
