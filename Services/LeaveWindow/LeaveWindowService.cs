using AutoCAC.Extensions;
using AutoCAC.Models;

namespace AutoCAC.Services;

public sealed class LeaveWindowService
{
    private const string SettingsGroup = "LeaveWindow";

    private readonly CacheService _cache;

    public LeaveWindowService(CacheService cache)
    {
        _cache = cache;
    }

    public async Task<LeaveWindow> GetWindowForDateAsync(DateTime? current = default)
    {
        DateTime now = current ?? DateTime.Now;
        DateOnly today = now.DateOnly;
        string cacheKey = $"LeaveWindow:Date:{today:yyyyMMdd}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            () => CalculateCurrentWindowAsync(today),
            watchDbTable: "AppSettings",
            durationMinutes: 120);
    }

    public async Task<LeaveWindow> GetWindowForRequestAsync(LeaveRequest request)
    {
        LeaveWindow window = await GetWindowForDateAsync(request.CreatedAt);

        if (!window.IsInWindow(request.CreatedAt)) return null;
        return window;
    }

    private async Task<LeaveWindow> CalculateCurrentWindowAsync(DateOnly today)
    {
        LeaveWindowOptions options = await GetLeaveWindowOptionsAsync();

        return CalculateCurrentWindow(options, today);
    }

    private Task<LeaveWindowOptions> GetLeaveWindowOptionsAsync()
    {
        return _cache.GetAppSettingAsync<LeaveWindowOptions>(SettingsGroup, true);
    }

    private static LeaveWindow CalculateCurrentWindow(
        LeaveWindowOptions options,
        DateOnly today)
    {
        DateOnly firstWindow1Start = options.FirstWindow1StartDate;
        if (today < options.FirstWindow1StartDate)
        {
            return new LeaveWindow();
        }
        int daysSinceFirstWindow1 =
            today.DayNumber - firstWindow1Start.DayNumber;

        int cycleIndex = daysSinceFirstWindow1 / options.CycleDays;

        DateOnly anchorDate =
            firstWindow1Start.AddDays(cycleIndex * options.CycleDays);

        DateOnly window1StartDate = anchorDate;

        DateOnly window1EndDate =
            anchorDate.AddDays(options.Window1EndOffsetDays);

        DateOnly window2StartDate =
            anchorDate.AddDays(options.Window2StartOffsetDays);

        DateOnly window2EndDate =
            anchorDate.AddDays(options.Window2EndOffsetDays);

        DateOnly leaveStartDate =
            anchorDate.AddDays(options.LeaveStartOffsetDays);

        DateOnly leaveEndDate =
            anchorDate.AddDays(options.LeaveEndOffsetDays);

        if (today >= window1StartDate && today <= window1EndDate)
        {
            return new LeaveWindow
            {
                WindowNumber = 1,
                WindowStart = window1StartDate.ToDateTime(options.WindowOpenTime),
                WindowEnd = window1EndDate.ToDateTime(options.WindowCloseTime),
                LeaveStartDate = leaveStartDate,
                LeaveEndDate = leaveEndDate
            };
        }

        if (today >= window2StartDate && today <= window2EndDate)
        {
            return new LeaveWindow
            {
                WindowNumber = 2,
                WindowStart = window2StartDate.ToDateTime(options.WindowOpenTime),
                WindowEnd = window2EndDate.ToDateTime(options.WindowCloseTime),
                LeaveStartDate = leaveStartDate,
                LeaveEndDate = leaveEndDate
            };
        }

        return new LeaveWindow
        {
            LeaveStartDate = leaveStartDate,
            LeaveEndDate = leaveEndDate
        };
    }
}