using System.Text.Json;

namespace AutoCAC.Models
{
    public partial class OrderDialog
    {
        public object ParsedItems
        {
            get
            {
                try
                {
                    return Type switch
                    {
                        "quick order" => string.IsNullOrWhiteSpace(Responses)
                            ? new List<ResponseItem>()
                            : JsonSerializer.Deserialize<List<ResponseItem>>(Responses) ?? new List<ResponseItem>(),

                        "menu" or "order set" or "dialog" => string.IsNullOrWhiteSpace(Items)
                            ? new List<ItemItem>()
                            : JsonSerializer.Deserialize<List<ItemItem>>(Items) ?? new List<ItemItem>(),

                        _ => new List<object>()
                    };
                }
                catch
                {
                    return new List<object>();
                }
            }
        }
    }

    public class ResponseItem
    {
        public string ItemEntry { get; set; }
        public string Dialog { get; set; }
        public string Instance { get; set; }
        public string Value { get; set; }
        public string Text { get; set; }
    }

    public class ItemItem
    {
        public string Item { get; set; }
        public string Mnemonic { get; set; }
        public string DisplayText { get; set; }
        public string DisplayOnly { get; set; }
    }
}


