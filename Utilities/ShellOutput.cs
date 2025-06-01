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
        public bool BufferFrozen { get; set; } = false;
        public string Echoed { get; private set; } = "";
        public string Buffered => _buffer.ToString();

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

        public void ClearBuffer()
        {
            if (!BufferFrozen)
            {
                _buffer.Clear();
            }
        }

        public string Prompt() => Buffered.LastLine();
        public string CurrentValue()
        {
            string lastStr = Buffered;
            int colonIdx = lastStr.IndexOf(':')+1;
            if (colonIdx<=0)
            {
                return "";
            }

            int replaceIdx = lastStr.LastIndexOf("Replace");
            if (replaceIdx>colonIdx)
            {
                return lastStr[colonIdx..replaceIdx].Trim();
            }

            int slashIdx = lastStr.LastIndexOf("//");
            if (slashIdx>colonIdx)
            {
                return lastStr[colonIdx..slashIdx].Trim();
            }

            return "";
        }

        private async Task WriteToTerminalAsync(string data)
        {
            await _js.WriteToXtermAsync(data);
        }
    }
}
