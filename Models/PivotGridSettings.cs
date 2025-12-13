namespace AutoCAC.Models
{
    public class PivotGridSettings
    {
        public List<string> RowFields { get; set; }
        public List<string> ColumnFields { get; set; }
        public List<PivotAggregateSetting> Aggregates { get; set; }
    }

    public class PivotAggregateSetting
    {
        public string Property { get; set; }           // e.g. "PricePerOrderUnit"
        public string Title { get; set; }              // e.g. "Total Cost"
        public string Aggregate { get; set; }          // e.g. "Sum", "Average"
        public string FormatString { get; set; }       // e.g. "{0:C}"
    }
}
