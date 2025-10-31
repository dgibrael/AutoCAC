using AutoCAC.Components.Templates;
using AutoCAC.Components.Templates.PatientSearch;
using AutoCAC.Components.Templates.StaffSearch;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Components;
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
        public static async Task<bool?> DeleteConfirm(
            this DialogService dialogService,
            string itemName = "item",
            string customMessage = null
            )
        {
            string title = "Delete?";
            if (customMessage == null)
            {
                customMessage = $"Are you sure you want to delete this {itemName}";
            }
            return await dialogService.Confirm(customMessage, title);
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

        /// <summary>
        /// Opens a reusable TItem dialog component and returns the saved item, or null if cancelled.
        /// Usage: await DialogService.EditAsync<MyTItemDialog<Customer>, Customer>(item, "Edit customer");
        /// </summary>
        public static async Task<TItem> EditAsync<TDialog, TItem>(
            this DialogService dialogService,
            TItem item,
            string title = "Edit",
            DialogOptions options = null)
            where TDialog : ComponentBase
            where TItem : class, new()
        {
            var parameters = new Dictionary<string, object>
            {
                ["Item"] = item
            };

            var result = await dialogService.OpenAsync<TDialog>(
                title,
                parameters,
                options ?? new DialogOptions
                {
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            return result as TItem;
        }
        public static async Task<AutoCAC.Models.AdUserDto> StaffAdDialog(
            this DialogService dialogService,
            string initialGroup = "NAV/CHC CSU Staff")
        {
            var parameters = new Dictionary<string, object>
            {
                { "GroupEquals", initialGroup }
            };

            var result = await dialogService.OpenAsync<StaffAdDialog>(
                title: "Staff Search",
                parameters: parameters,
                options: new DialogOptions
                {
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            return result as AutoCAC.Models.AdUserDto;
        }
        public static async Task<AutoCAC.Models.Patient> PatientSelectDialogAsync(
            this DialogService dialogService,
            DialogOptions options = null)
        {
            var result = await dialogService.OpenAsync<PatientDialogDataGrid>(
                title: "Patient Search",
                options: options ?? new DialogOptions
                {
                    AutoFocusFirstElement = true,
                    Width = "75%",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            return result as AutoCAC.Models.Patient;
        }
        public static async Task<string> AutoCompleteDialogAsync(
            this DialogService dialogService,
            string value,
            IEnumerable<string> suggestions,
            string title = "Enter Text")
        {
            var result = await dialogService.OpenAsync<AutoCompleteDialog>(
                title,
                new Dictionary<string, object>
                {
                    { "Suggestions", suggestions },
                    { "InitialValue", value }
                });
            return result as string;
        }
    }
}


