using AutoCAC.Extensions;
using System.Text;
using Newtonsoft.Json;
using Microsoft.JSInterop;
namespace AutoCAC.Utilities
{
    public class ShellOutput
    {
        private readonly IJSRuntime _js;
        public ShellOutput(IJSRuntime js)
        {
            _js = js;
        }
        private readonly StringBuilder _buffer = new();

        public string Echoed { get; private set; } = "";
        public string Received { get; private set; } = "";
        public string Buffered => _buffer.ToString();

        public void SetReceived(string data)
        {
            Received = data;
            _ = WriteToTerminalAsync(data);
        }

        public void SetEchoed(string data)
        {
            Echoed = data;
            _ = WriteToTerminalAsync(data);
        }

        public void Append(string data)
        {
            _buffer.Append(data);
            _ = WriteToTerminalAsync(data);
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
            builder
                .Replace('"', '\'')
                .Replace('\x1F', '"')
                .Replace("\f", "")
                .Replace("\\", "&#92")
                .Replace("\n", "")
                .Replace("\r", "");

            string json = builder.ToString();
            //_ = WriteToTerminalAsync(json.LastLine());
            ClearBuffer();
            return json;
        }

        public IEnumerable<T> BufferToObject<T>()
        {
            string json = BufferToJson();
            return JsonConvert.DeserializeObject<IEnumerable<T>>(json) ?? new List<T>();
        }
        public IEnumerable<IDictionary<string, object>> BufferToObject()
        {
            return BufferToObject<IDictionary<string, object>>();
        }

        public string Prompt() => Received.LastLine();

        private async Task WriteToTerminalAsync(string data)
        {
            await _js.WriteToXtermAsync(data);
        }
    }
}
