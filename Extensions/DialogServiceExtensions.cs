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
            string initial = null,
            int maxLength = 255,
            string placeholder = null,
            string message = null,
            DialogOptions options = null)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["Header"] = header ?? "Enter Text",
                ["Message"] = message,
                ["InitialValue"] = initial,
                ["Placeholder"] = placeholder,
                ["MaxLength"] = maxLength
            };

            var result = await dialogService.OpenAsync<Components.Templates.TextDialog>(
                title ?? "Prompt",
                parameters,
                options ?? new DialogOptions
                {
                    Width = "480px",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            return result as string;
        }

    }
}


