using AutoCAC.Common;
using AutoCAC.Extensions;
using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCAC.Services;

public sealed class ActivityLogService
{
    private readonly IDbContextFactory<MainContext> _contextFactory;

    public ActivityLogService(
        IDbContextFactory<MainContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<TActivity> LogActivityAsync<TActivity, TItemKey>(
        ActivityLogType activityType,
        TItemKey itemId,
        int? authUserId,
        string message = "",
        string changedField = null,
        Action<TActivity> configure = null
        )
        where TActivity : class, IActivityLog<TItemKey>, new()
    {
        var activity = new TActivity
        {
            ActivityAt = DateTime.Now,
            ActivityTypeEnum = activityType,
            AuthUserId = authUserId,
            Message = message,
            ItemId = itemId
        };
        if (changedField != null)
        {
            activity.ChangedField = changedField;
        }
        if (configure != null) configure(activity);

        await _contextFactory.AddItemAsync(activity);

        return activity;
    }

    public Task<TActivity> CreatedAsync<TActivity, TItemKey>(
        TItemKey itemId,
        int? authUserId)
         where TActivity : class, IActivityLog<TItemKey>, new()
    {
        return LogActivityAsync<TActivity, TItemKey>(ActivityLogType.Created, itemId, authUserId: authUserId);
    }
    public Task<TActivity> CommentAsync<TActivity, TItemKey>(
        TItemKey itemId,
        int authUserId,
        string comment)
        where TActivity : class, IActivityLog<TItemKey>, new()
    {
        return LogActivityAsync<TActivity, TItemKey>(ActivityLogType.Comment, itemId, authUserId: authUserId, message: comment);
    }
    public Task<TActivity> ValueChangedAsync<TActivity, TItemKey>(
        TItemKey itemId,
        string newValue,
        int? authUserId,
        string changedField = null)
        where TActivity : class, IActivityLog<TItemKey>, new()
    {
        return LogActivityAsync<TActivity, TItemKey>(ActivityLogType.ValueChanged, itemId, authUserId: authUserId, message: newValue, changedField: changedField);
    }
}
