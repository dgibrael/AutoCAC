using AutoCAC.Extensions;
using System.Text;
using Newtonsoft.Json;

namespace AutoCAC.Utilities
{
    public class ShellOutput
    {
        private readonly StringBuilder _buffer = new();

        public string Echoed { get; set; } = "";
        public string Received { get; set; } = "";
        public string Buffered => _buffer.ToString();

        public void Append(string data)
        {
            _buffer.Append(data);
        }

        public void ClearBuffer() => _buffer.Clear();

        public string BufferToJson()
        {
            string buffer = Buffered;

            if (string.IsNullOrEmpty(buffer))
                return "";

            var span = buffer.AsSpan();

            int lastClose = span.LastIndexOf('}');
            if (lastClose < 0) return "";

            int firstOpen = span.IndexOf('{');
            if (firstOpen < 0) return "";

            var trimmedSpan = span[firstOpen..(lastClose + 1)];

            var builder = new StringBuilder();
            builder.Append('[');
            builder.Append(trimmedSpan);
            builder.Append(']');

            string json = builder.ToString();

            json = json
                .Replace('"', '\'')
                .Replace('\x1F', '"')
                .Replace("\f", "")
                .Replace("\\", "&#92");

            ClearBuffer();
            return json;
        }

        public List<T> BufferToObject<T>()
        {
            string json = BufferToJson();
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        public string Prompt() => Received.LastLine();

    }
}
