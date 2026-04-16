using AutoCAC.Extensions;
using AutoCAC.Models;
using AutoCAC.Utilities;
using Microsoft.EntityFrameworkCore;

namespace AutoCAC.Services.TaskScheduling;

public class ScheduledTaskScheduler : BackgroundService, IDisposable
{
    private readonly IDbContextFactory<mainContext> _dbContextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScheduledTaskScheduler> _logger;
    private readonly EmailService _emailService;
    private readonly SemaphoreSlim _refreshSignal = new(0, 1);
    private SqlWatcher _scheduledTaskScheduleWatcher;
    private bool _disposed;

    public ScheduledTaskScheduler(
        IDbContextFactory<mainContext> dbContextFactory,
        IConfiguration configuration,
        ILogger<ScheduledTaskScheduler> logger,
        EmailService emailService
        )
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
        _logger = logger;
        _emailService = emailService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RebuildNextRunTimesAsync(stoppingToken);
        StartWatchers();

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime? nextRunAt = await RunDueTasksAndGetNextRunAtAsync(stoppingToken);

            Task delayTask = nextRunAt.HasValue
                ? Task.Delay(GetDelayUntil(nextRunAt.Value), stoppingToken)
                : Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);

            Task refreshTask = _refreshSignal.WaitAsync(stoppingToken);

            await Task.WhenAny(delayTask, refreshTask);
        }
    }

    private async Task RebuildNextRunTimesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        List<ScheduledTask> tasks = await db.ScheduledTasks
            .Include(x => x.ScheduledTaskSchedules)
            .ToListAsync(cancellationToken);

        foreach (ScheduledTask scheduledTask in tasks)
        {
            DateTime baseline = scheduledTask.LastRunAt ?? DateTime.Now;

            scheduledTask.NextRunAt = scheduledTask.ScheduledTaskSchedules
                .Select(x => (DateTime?)x.GetNextRunAt(baseline))
                .Min();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void StartWatchers()
    {
        string connectionString = _configuration.GetConnectionString("mainConnection");

        _scheduledTaskScheduleWatcher = new SqlWatcher(
            connectionString,
            """
            SELECT COUNT_BIG(*)
            FROM dbo.ScheduledTaskSchedule
            """);

        _scheduledTaskScheduleWatcher.ChangedAsync += OnWatchedTableChangedAsync;
    }

    private async Task OnWatchedTableChangedAsync()
    {
        if (_refreshSignal.CurrentCount == 0)
        {
            await RebuildNextRunTimesAsync();
            _refreshSignal.Release();
        }
    }

    private async Task<DateTime?> RunDueTasksAndGetNextRunAtAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime now = DateTime.Now;

        List<ScheduledTask> dueTasks = await db.ScheduledTasks
            .Where(x => x.NextRunAt != null && x.NextRunAt <= now)
            .Include(x => x.ScheduledTaskSchedules)
            .OrderBy(x => x.NextRunAt)
            .ToListAsync(cancellationToken);

        foreach (ScheduledTask scheduledTask in dueTasks)
        {
            await RunTask(scheduledTask, cancellationToken);
            switch (scheduledTask.FailureCount)
            {
                case 0:
                    scheduledTask.LastRunAt = now;
                    scheduledTask.NextRunAt = scheduledTask.ScheduledTaskSchedules
                        .Select(x => (DateTime?)x.GetNextRunAt(now))
                        .Min();
                    break;
                case > 5:
                    scheduledTask.NextRunAt = null;
                    _logger.LogError("Scheduled task failed > 5 times. Task name: {TaskName}", scheduledTask.Name);
                    break;
                default:
                    scheduledTask.NextRunAt = now.AddMinutes(5 * scheduledTask.FailureCount);
                    break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return await db.ScheduledTasks
            .Where(x => x.NextRunAt != null)
            .MinAsync(x => (DateTime?)x.NextRunAt, cancellationToken);
    }

    private async Task RunTask(ScheduledTask scheduledTask, CancellationToken cancellationToken)
    {
        try
        {
            switch (scheduledTask.HandlerKeyEnum)
            {
                case ScheduledTaskHandlerKey.GbReport:
                    await GbReportAsync(cancellationToken);
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            scheduledTask.FailureCount += 1;
            return;
        }
        scheduledTask.FailureCount = 0;
    }

    private async Task GbReportAsync(CancellationToken cancellationToken)
    {
        PeriodSelection periodSelection = new PeriodSelection(PeriodOption.LastQuarter);
        var start = periodSelection.CurrentStart.StartOfDay;
        var end = periodSelection.CurrentEnd.StartOfNextDay;
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var paid = await db.VwBillingRxes
            .Where(x => x.FillDate >= start && x.FillDate < end && x.Division == "CHINLE HOSP PHARMACY")
            .SumAsync(x => x.TotalPaid);
        var fills = await db.RxFills
            .Where(x => x.ReleasedDateTime >= start && x.ReleasedDateTime < end && x.Rx.Division == "CHINLE HOSP PHARMACY")
            .CountAsync();
        var msg = $"<ul><li>Total Revenue: {paid}</li><li>Total Rx Fills: {fills}</li><ul>";
        await _emailService.SendEmailByGroupsAsync($"GB Data for {start.DateOnly} to {end.DateOnly}", msg, "GbReport");
    }

    private static TimeSpan GetDelayUntil(DateTime nextRunAt)
    {
        TimeSpan delay = nextRunAt - DateTime.Now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            base.Dispose();
            return;
        }

        _disposed = true;
        _scheduledTaskScheduleWatcher?.Dispose();
        _refreshSignal.Dispose();

        base.Dispose();
    }
}