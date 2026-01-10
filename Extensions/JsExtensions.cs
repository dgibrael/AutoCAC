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
        
        public static async Task DownloadString(this IJSRuntime js, string data, string filename = "output.txt", string mimeType = "text/plain", bool addUtf8Bom = false)
        {
            await js.InvokeVoidAsync("downloadTextFile", data, filename, mimeType, addUtf8Bom);
        }

        public static async Task DownloadExcel(this IJSRuntime js, string base64, string fileName = "data.xlsx")
        {
            await js.InvokeVoidAsync("downloadExcel", base64, fileName);
        }

        public static async Task DownloadFileFromStream(this IJSRuntime js, DotNetStreamReference streamRef, string fileName = "data.csv", string mimeType = "text/csv;charset=utf-8")
        {
            await js.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef, mimeType);
        }

        public static async Task DialogShow(this IJSRuntime js, string dialogId = "RPMSOutputDiv")
        {
            await js.InvokeVoidAsync("showDialog", dialogId);
        }

        public static async Task DialogHide(this IJSRuntime js, string dialogId = "RPMSOutputDiv")
        {
            await js.InvokeVoidAsync("hideDialog", dialogId);
        }

        public static async Task CopyText(this IJSRuntime js, string txt)
        {
            await js.InvokeVoidAsync("navigator.clipboard.writeText", txt);
        }
        public static async Task<int> GetWindowHeight(this IJSRuntime js)
        {
            return await js.InvokeAsync<int>("getWindowHeight");
        }
        public static ValueTask<string> GetChatDraftText(this IJSRuntime js, string elementId)
            => js.InvokeAsync<string>("GetChatDraftText", elementId);
    }
}
