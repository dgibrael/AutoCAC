#nullable enable
namespace AutoCAC.Models;

using AutoCAC.Common;
public interface IActivityLog
{
    long Id { get; set; }
    string ActivityType { get; set; }
    int? AuthUserId { get; set; }
    AuthUser? AuthUser { get; set; }
    DateTime ActivityAt { get; set; }
    string Message { get; set; }
    ActivityLogType ActivityTypeEnum
    {
        get
        {
            return Enum.TryParse<ActivityLogType>(
                ActivityType,
                true,
                out var value)
                ? value
                : ActivityLogType.Unknown;
        }

        set => ActivityType = value.ToString();
    }
    bool IsCurrentUser(int currentUserId) => AuthUserId.HasValue && AuthUserId.Value == currentUserId;
    string DisplayMessage => ActivityTypeEnum switch
    {
        ActivityLogType.Created =>
            "Created" + (!string.IsNullOrWhiteSpace(Message)
                ? $" ({Message})"
                : ""),
        ActivityLogType.StatusChanged => "Status Changed to: " + Message,
        ActivityLogType.Error => "Error: " + Message,
        ActivityLogType.Warning => "Warning: " + Message,
        _ => Message ?? ""
    };
}
