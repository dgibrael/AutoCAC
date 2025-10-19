using Radzen;

namespace AutoCAC.Extensions
{
    public static class NotificationServiceExtensions
    {
        public static void Success(this NotificationService notificationService, string message, string title = "Success")
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = title,
                Detail = message,
                Duration = 3000
            });
        }
        public static void Error(this NotificationService notificationService, Exception ex)
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = ex.GetType().Name,
                Detail = ex.Message,
                Duration = 5000
            });
        }
    }
}
