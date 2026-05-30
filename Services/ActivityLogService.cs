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

    public async Task<T> LogActivityAsync<T>(
        ActivityLogType activityType,
        Action<T> configure,
        int? authUserId = null,
        string message = "",
        string changedField = null
        )
        where T : class, IActivityLog, new()
    {
        var activity = new T
        {
            ActivityAt = DateTime.Now,
            ActivityTypeEnum = activityType,
            AuthUserId = authUserId,
            Message = message
        };
        configure(activity);

        await _contextFactory.AddItemAsync(activity);

        return activity;
    }

    public Task<T> CreatedAsync<T>(
        int authUserId,
        Action<T> configure)
        where T : class, IActivityLog, new()
    {
        return LogActivityAsync<T>(ActivityLogType.Created, configure, authUserId: authUserId);
    }
    public Task<T> CommentAsync<T>(
        int authUserId,
        string comment,
        Action<T> configure)
        where T : class, IActivityLog, new()
    {
        return LogActivityAsync<T>(ActivityLogType.Comment, configure, authUserId: authUserId, message: comment);
    }
}
