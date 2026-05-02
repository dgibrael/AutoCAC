using AutoCAC.Components.Templates;
using AutoCAC.Components.Templates.DataGrids;
using AutoCAC.Components.Templates.DrugSearch;
using AutoCAC.Components.Templates.Forms;
using AutoCAC.Components.Templates.PatientSearch;
using AutoCAC.Components.Templates.StaffSearch;
using AutoCAC.Models;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Radzen;
using Radzen.Blazor;
using System.Linq.Expressions;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoCAC.Extensions
{
    public static class DialogServiceExtensions
    {
        public static async Task<bool?> YesNoDialog(
            this DialogService dialogService,
            string message = "Are you sure?",
            string title = "Confirm",
            string okText = "Yes",
            string cancelText = "No"
            )
        {
            return await dialogService.Confirm(message, title, new ConfirmOptions
            {
                OkButtonText = okText,
                CancelButtonText = cancelText
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
            DialogOptions options = null,
            string mask = null,
            string characterPattern = null
            )
        {
            var parameters = new Dictionary<string, object>
            {
                ["Header"] = string.IsNullOrWhiteSpace(header) ? "Enter Text" : header,
                ["Message"] = message,
                ["InitialValue"] = initial,
                ["Placeholder"] = placeholder,
                ["MaxLength"] = maxLength,
                ["DisallowedChars"] = disallowedChars,
                ["Mask"] = mask,
                ["CharacterPattern"] = characterPattern
            };

            var result = await dialogService.OpenAsync<Components.Templates.TextDialog>(
                string.IsNullOrWhiteSpace(title) ? "Prompt" : title,
                parameters,
                options ?? new DialogOptions
                {
                    AutoFocusFirstElement = true,
                    Width = "75%",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            // OK → string (may be ""), Cancel → null
            return result as string;
        }

        public static async Task<string> Ndc(this DialogService dialogService, string initial = "")
        {
            return await dialogService.TextPromptAsync("Enter NDC", "Enter 11 digit ndc below to continue", mask: "*****-****-**", characterPattern: "[0-9]", initial: initial);
        }

        public static async Task<TValue?> NumericPromptAsync<TValue>(
            this DialogService dialogService,
            string title = "Enter Value",
            string header = "Enter Value",
            TValue? initial = null,
            decimal? max = null,
            decimal? min = 0,
            string placeholder = "",
            DialogOptions options = null) 
            where TValue : struct, INumber<TValue>
        {
            var parameters = new Dictionary<string, object>
            {
                ["Header"] = header,
                ["InitialValue"] = initial,
                ["Placeholder"] = placeholder,
                ["Max"] = max,
                ["Min"] = min
            };

            var result = await dialogService.OpenAsync<Components.Templates.NumericDialog<TValue>>(
                title,
                parameters,
                options ?? new DialogOptions
                {
                    AutoFocusFirstElement = true,
                    Width = "75%",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });
            if (result is TValue value)
                return value;

            return null;
        }

        public static Task<int?> NumericPromptAsync(
            this DialogService dialogService,
            string title = "Prompt",
            string header = "Enter Value",
            int? initial = null,
            decimal? max = null,
            decimal? min = 0,
            string placeholder = "",
            DialogOptions options = null)
            => dialogService.NumericPromptAsync<int>(title, header, initial, max, min, placeholder, options);

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
        public static async Task<AutoCAC.Models.AuthUser> StaffAdDialog(
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
            return result as AutoCAC.Models.AuthUser;
        }
        public static async Task<AutoCAC.Models.AuthUser> StaffSearch(
            this DialogService dialogService)
        {
            var result = await dialogService.OpenAsync<AuthUserSelect>(
                title: "Staff Search",
                options: new DialogOptions
                {
                    AutoFocusFirstElement = true,
                    Width = "75%",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });
            return result as AutoCAC.Models.AuthUser;
        }

        public static DialogOptions GetDefaultOptions(this DialogService dialogService) => new()
        {
            AutoFocusFirstElement = true,
            Width = "75%",
            CloseDialogOnOverlayClick = true,
            Resizable = true,
            Draggable = true
        };

        public static async Task<TItem> OpenDefaultAsync<TTemplate, TItem>(
            this DialogService dialogService,
            string title = "",
            Dictionary<string, object> parameters = null
            ) 
            where TTemplate : ComponentBase 
            where TItem : class, new()
        {
            var result = await dialogService.OpenAsync<TTemplate>(
                title: title,
                parameters: parameters,
                options: dialogService.GetDefaultOptions());
            return result is TItem typed ? typed : null;
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
        public static async Task<AutoCAC.Models.Drug> DrugSelectDialogAsync(
            this DialogService dialogService,
            Expression<Func<Drug, bool>> whereExpression = null,
            DialogOptions options = null)
        {
            var result = await dialogService.OpenAsync<DrugDialogDataGrid>(
                title: "Select a Drug from below",
                parameters: new Dictionary<string, object> { ["WherePredicate"] = whereExpression },
                options: options ?? new DialogOptions
                {
                    AutoFocusFirstElement = true,
                    Width = "75%",
                    CloseDialogOnOverlayClick = true,
                    Resizable = true,
                    Draggable = true
                });

            return result as AutoCAC.Models.Drug;
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
        public static async Task<string> ListDialogAsync(
            this DialogService dialogService,
            HashSet<string> choices,
            string header = "",
            string title = "Select from below")
        {
            var result = await dialogService.OpenAsync<ListDialog>(
                title,
                new Dictionary<string, object>
                {
                    { "Choices", choices },
                    { "Header", header }
                });
            return result as string;
        }
        public static async Task<bool> ProceedAnyway(
            this DialogService dialogService,
            string msg = "This action is not recommended. Are you sure you wish to proceed?",
            string title = "Warning"
            )
        {

            var result = await dialogService.Confirm(
            msg,
            title,
            new ConfirmOptions() { OkButtonText = "No (Recommended)", CancelButtonText = "Yes, continue anyway (NOT Recommended)"});
            return result == false;
        }

        public static async Task ProgressBarDialog(
            this DialogService dialogService,
            Func<IProgress<double>, Task> work)
        {
            var parameters = new Dictionary<string, object>
            {
                ["Work"] = work
            };

            var options = new DialogOptions
            {
                ShowClose = false,
                CloseDialogOnOverlayClick = false
            };

            await dialogService.OpenAsync<ProgressBarDialog>(
                title: "Loading...",
                parameters: parameters,
                options: options);
        }

        public static void ShowLoadingSpinner(this DialogService dialogService, string LoadMessage = "Loading...")
        {
            var options = new DialogOptions
            {
                ShowClose = false,
                CloseDialogOnOverlayClick = false,
                Draggable = false,
                Resizable = false
            };
            // Fire-and-forget intentionally; suppress CS4014 correctly
            _ = dialogService.OpenAsync<LoadingDialog>("", options: options
                , parameters: new Dictionary<string, object>
                {
                    { "LoadMessage", LoadMessage },
                });
        }

        public static async Task<TItem> DataGridSelectAsync<TItem>(
            this DialogService dialogService,
            Func<AutoCAC.Models.MainContext, IQueryable<TItem>> queryFactory = null,
            IEnumerable<string> includeColumns = null,
            IEnumerable<string> excludeColumns = null,
            string[] searchColumns = null,
            string header = null
            )
            where TItem : class
        {
            var parameters = new Dictionary<string, object>();
            DataGridHelper<TItem> gridModel = new()
            {
                QueryFactory = queryFactory,
                IncludeColumns = includeColumns,
                ExcludeColumns = excludeColumns,
                SearchColumns = searchColumns,
            };
           
            parameters[nameof(DataGridDialog<TItem>.GridModel)] = gridModel;

            var options = new DialogOptions
            {
                AutoFocusFirstElement = true,
                Width = "75%",
                CloseDialogOnOverlayClick = true,
                Resizable = true,
                Draggable = true
            };

            var result = await dialogService.OpenAsync<DataGridDialog<TItem>>(
                title: header ?? $"Select {typeof(TItem).Name}",
                parameters: parameters,
                options: options
                );

            return result as TItem;
        }

        public static async Task DataGridViewAsync<TItem>(
            this DialogService dialogService,
            Func<AutoCAC.Models.MainContext, IQueryable<TItem>> queryFactory = null,
            IEnumerable<string> includeColumns = null,
            IEnumerable<string> excludeColumns = null,
            string[] searchColumns = null,
            string header = null
            )
            where TItem : class
        {
            var parameters = new Dictionary<string, object>();
            DataGridHelper<TItem> gridModel = new()
            {
                QueryFactory = queryFactory,
                IncludeColumns = includeColumns,
                ExcludeColumns = excludeColumns,
                SearchColumns = searchColumns,
            };

            parameters[nameof(DataGridDialog<TItem>.GridModel)] = gridModel;
            parameters["ViewOnly"] = true;
            var options = new DialogOptions
            {
                AutoFocusFirstElement = true,
                Width = "75%",
                CloseDialogOnOverlayClick = true,
                Resizable = true,
                Draggable = true
            };

            await dialogService.OpenAsync<DataGridDialog<TItem>>(
                title: header ?? $"Select {typeof(TItem).Name}",
                parameters: parameters,
                options: options
                );
        }

        public static async Task ViewComments(
            this DialogService dialogService,
            IEnumerable<CommentModel> Comments,
            string title = "Comments"
            )
        {
            var parameters = new Dictionary<string, object>()
            {
                ["Comments"] = Comments
            };
            var options = new DialogOptions
            {
                Width = "75%",
                CloseDialogOnOverlayClick = true,
                Resizable = true,
                Draggable = true,
                ShowClose = true,
                ShowTitle = true,
            };

            await dialogService.OpenAsync<CommentRead>(
                title: title,
                parameters: parameters,
                options: options
                );
        }

        public static async Task<TItem> OpenFormAsync<TItem>(
            this DialogService dialogService,
            RenderFragment<TItem> childContent,
            TItem data,
            string title = "",
            bool saveToDb = true
            )
            where TItem : class, new()
        {
            var parameters = new Dictionary<string, object>
            {
                ["ChildContent"] = childContent,
                ["Item"] = data,
                ["SaveToDb"] = saveToDb
                // Item intentionally omitted; your component will create one when Item is null
            };
            var options = new DialogOptions
            {
                Width = "75%",
                CloseDialogOnOverlayClick = true,
                Resizable = true,
                Draggable = true,
                ShowClose = true,
                ShowTitle = true,
            };
            var result = await dialogService.OpenAsync<VanillaDialogForm<TItem>>(
                title: title,
                parameters: parameters,
                options: options
                );

            return result is TItem typed ? typed : null;
        }
        public static async Task<TItem> OpenAutoFormAsync<TItem>(
            this DialogService dialogService,
            TItem Data = null,
            HashSet<string> IncludedProperties = default,
            bool SaveToDb = false,
            string title = "",
            IEnumerable<SplitButtonItem> OtherActions = null
            )
            where TItem : class, new()
        {
            var parameters = new Dictionary<string, object>
            {
                ["Data"] = Data,
                ["IncludedProperties"] = IncludedProperties,
                ["SaveToDb"] = SaveToDb,
                ["OtherActions"] = OtherActions
            };
            var options = new DialogOptions
            {
                AutoFocusFirstElement = true,
                Width = "75%",
                CloseDialogOnOverlayClick = true,
                Resizable = true,
                Draggable = true,
                ShowClose = true,
                ShowTitle = true,
            };
            var result = await dialogService.OpenAsync<AutoFormDialog<TItem>>(
                title: title,
                parameters: parameters,
                options: options
                );

            return result is TItem typed ? typed : null;
        }
    }
}


