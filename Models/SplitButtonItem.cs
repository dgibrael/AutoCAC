namespace AutoCAC.Models
{
    public sealed class SplitButtonItem
    {
        public string Text { get; set; } = "";
        public string Value { get; set; } = "";
        public string Icon { get; set; }
        public string IconColor { get; set; }
        public Func<bool> Disabled { get; set; }
        public Func<bool> Visible { get; set; }
        public Func<Task> Action { get; set; }
    }
}
