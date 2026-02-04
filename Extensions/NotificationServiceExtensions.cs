using Radzen;

namespace AutoCAC.Extensions
{
    public static class NotificationServiceExtensions
    {
        public static void Success(this NotificationService notificationService, string message = "Success", string title = "Success")
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = title,
                Detail = message,
                Duration = 3000
            });
        }

        public static void Info(this NotificationService notificationService, string message = "Updating...", string title = "")
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Info,
                Summary = title,
                Detail = message,
                Duration = 3000
            });
        }

        public static void Error(this NotificationService notificationService, Exception ex, string customMsg = null)
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = ex.GetType().Name,
                Detail = customMsg ?? $"Something went wrong (error code: {ex.HResult})",
                Duration = 5000
            });
        }
        public static void Error(this NotificationService notificationService, string msg = "Something went wrong")
        {
            notificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Error",
                Detail = msg,
                Duration = 10000
            });
        }
        /// <summary>
        /// Executes the async action and notifies success or error.
        /// </summary>
        public static async Task<bool> TryNotify(this NotificationService notificationService,
            Func<Task> action,
            string successMessage = "Success",
            string successTitle = "Success",
            bool rethrow = false,
            string customErrorMsg = null
            )
        {
            try
            {
                await action();
                notificationService.Success(successMessage, successTitle);
                return true;
            }
            catch (OperationCanceledException)
            {
                // Optional: ignore cancellations (don’t notify as error).
                return false;
            }
            catch (Exception ex)
            {
                if (customErrorMsg is not null)
                {
                    notificationService.Error(customErrorMsg);
                }
                else
                {
                    notificationService.Error(ex);
                }
                if (rethrow) throw;
                return false;
            }
        }

        /// <summary>
        /// Executes the async function and notifies success or error.
        /// Returns the function's result.
        /// </summary>
        public static async Task<T> TryNotify<T>(this NotificationService notificationService,
            Func<Task<T>> action,
            string successMessage = "Success",
            string successTitle = "Success",
            bool rethrow = false)
        {
            try
            {
                var result = await action();
                notificationService.Success(successMessage, successTitle);
                return result;
            }
            catch (OperationCanceledException)
            {
                // Optional: ignore cancellations (don’t notify as error).
                return default(T);
            }
            catch (Exception ex)
            {
                notificationService.Error(ex);
                if (rethrow) throw;
                return default(T);
            }
        }
    }
}
