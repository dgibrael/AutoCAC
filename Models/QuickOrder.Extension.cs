using System.Text.Json;

namespace AutoCAC.Models
{
    public partial class QuickOrder
    {
        public List<ResponseItem> ParsedResponses =>
            string.IsNullOrWhiteSpace(Responses)
                ? new()
                : JsonSerializer.Deserialize<List<ResponseItem>>(Responses) ?? new();
    }

    public class ResponseItem
    {
        public string ItemEntry { get; set; }
        public string Dialog { get; set; }
        public string Instance { get; set; }
        public string Value { get; set; }
        public string Text { get; set; }
    }

}
