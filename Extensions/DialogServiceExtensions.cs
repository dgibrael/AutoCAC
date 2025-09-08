using Radzen;
using System.Threading.Tasks;

namespace AutoCAC.Extensions
{
    public static class DialogServiceExtensions
    {
        public static async Task<bool?> YesNoDialog(
            this DialogService dialogService,
            string message = "Are you sure?",
            string title = "Confirm")
        {
            return await dialogService.Confirm(message, title, new ConfirmOptions
            {
                OkButtonText = "Yes",
                CancelButtonText = "No"
            });
        }
        /// <summary>
        /// Shows a simple text prompt dialog and returns the entered value (null if cancelled).
        /// </summary>
        public static async Task<string> TextPromptAsync(
            this DialogService dialogService,
            string title = "Prompt",
            string header = "Enter Text",
            string initial = "",
            int maxLength = 255,
            string placeholder = "",
            string message = "",
            string disallowedChars = "",
            DialogOptions options = null)
        {
            var parameters = new Dictionary<string, object>
            {
                ["Header"] = string.IsNullOrWhiteSpace(header) ? "Enter Text" : header,
                ["Message"] = message,
                ["InitialValue"] = initial,
                ["Placeholder"] = placeholder,
                ["MaxLength"] = maxLength,
                ["DisallowedChars"] = disallowedChars
            };

            var result = await dialogService.OpenAsync<Components.Templates.TextDialog>(
                string.IsNullOrWhiteSpace(title) ? "Prompt" : title,
                parameters,
                options ?? new DialogOptions
                {
                    Width = "75%",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            // OK → string (may be ""), Cancel → null
            return result as string;
        }
    }
}


