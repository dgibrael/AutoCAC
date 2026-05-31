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
    string? ChangedField { get; set; }
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
}
public interface IActivityLog<TItemKey> : IActivityLog
{
    TItemKey ItemId { get; set; }
}
