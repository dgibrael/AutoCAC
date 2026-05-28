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
        return _cache.GetAppSettingsGroupAsync<LeaveWindowOptions>(SettingsGroup);
    }

    private static LeaveWindow CalculateCurrentWindow(
        LeaveWindowOptions options,
        DateOnly today)
    {
        DateOnly firstWindow1Start = options.StartDate
            .AddDays(options.Window1StartOffsetDays);

        int daysSinceFirstWindow1 = today.DayNumber - firstWindow1Start.DayNumber;
        int cycleIndex = FloorDivide(daysSinceFirstWindow1, options.CycleDays);

        DateOnly window1StartDate = firstWindow1Start
            .AddDays(cycleIndex * options.CycleDays);

        DateOnly leaveStartDate = options.StartDate
            .AddDays(cycleIndex * options.CycleDays);

        DateOnly leaveEndDate = leaveStartDate
            .AddDays(options.LeaveEndOffsetDays);

        DateOnly window1EndDate = window1StartDate
            .AddDays(options.Window1LengthDays - 1);

        DateOnly window2StartDate = window1EndDate
            .AddDays(options.Window2GapAfterWindow1Days + 1);

        DateOnly window2EndDate = window2StartDate
            .AddDays(options.Window2LengthDays - 1);

        DateTime window1Start = window1StartDate.ToDateTime(options.WindowOpenTime);
        DateTime window1End = window1EndDate.ToDateTime(options.WindowCloseTime);

        DateTime window2Start = window2StartDate.ToDateTime(options.WindowOpenTime);
        DateTime window2End = window2EndDate.ToDateTime(options.WindowCloseTime);



        if (today >= window1StartDate && today <= window1EndDate)
        {
            return new LeaveWindow
            {
                WindowNumber = 1,
                WindowStart = window1Start,
                WindowEnd = window1End,
                LeaveStartDate = leaveStartDate,
                LeaveEndDate = leaveEndDate
            };
        }

        if (today >= window2StartDate && today <= window2EndDate)
        {
            return new LeaveWindow
            {
                WindowNumber = 2,
                WindowStart = window2Start,
                WindowEnd = window2End,
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

    private static int FloorDivide(int value, int divisor)
    {
        int result = value / divisor;

        if (value < 0 && value % divisor != 0)
            result--;

        return result;
    }
}