namespace GrowWhatever.Core
{
    public sealed class FarmPlot
    {
        public string Id { get; }
        public int Row { get; }
        public int Column { get; }
        public string GrowableId { get; internal set; }
        public double Progress { get; internal set; }
        public bool IncludeInBulkHarvest { get; internal set; } = true;
        public bool IsEmpty => GrowableId == null;

        internal FarmPlot(int row, int column)
        {
            Row = row;
            Column = column;
            Id = $"plot-{row}-{column}";
        }
    }
}
