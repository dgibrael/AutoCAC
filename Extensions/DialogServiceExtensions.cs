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
    }
}


