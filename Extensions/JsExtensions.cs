using Microsoft.JSInterop;

namespace AutoCAC.Extensions
{
    public static class JsExtensions
    {
        public static async Task WriteToXtermAsync(this IJSRuntime js, string message)
        {
            await js.InvokeVoidAsync("writeRPMSXterm", message);
        }

        public static async Task ClearXtermAsync(this IJSRuntime js)
        {
            await js.InvokeVoidAsync("clearRPMSXterm");
        }

        public static async Task DownloadXtermContentAsync(this IJSRuntime js)
        {
            await js.InvokeVoidAsync("downloadRPMSContent");
        }

        public static async Task ScrollTo(this IJSRuntime js, string elementId = null)
        {
            if (!string.IsNullOrEmpty(elementId))
            {
                await js.InvokeVoidAsync("scrollToElement", elementId);
            }
            else
            {
                await js.InvokeVoidAsync("scrollToBottom");
            }
        }

        public static async Task ReinitXterm(this IJSRuntime js)
        {
            await js.InvokeVoidAsync("reinitRPMSXterm");
        }        
        
        public static async Task DownloadString(this IJSRuntime js, string data, string filename = "output.txt", string mimeType = "text/plain")
        {
            await js.InvokeVoidAsync("downloadTextFile", data, filename, mimeType);
        }

        public static async Task DownloadExcel(this IJSRuntime js, string base64, string fileName = "data.xlsx")
        {
            await js.InvokeVoidAsync("downloadExcel", base64, fileName);
        }

    }
}
