using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AutoCAC.Services.TaskScheduling;

public class ScheduledTaskScheduler : BackgroundService, IDisposable
{
    private readonly IDbContextFactory<mainContext> _dbContextFactory;
    private readonly IConfiguration _configuration;

    private readonly SemaphoreSlim _refreshSignal = new(0, 1);
    private SqlWatcher _scheduledTaskScheduleWatcher;
    private bool _disposed;

    public ScheduledTaskScheduler(
        IDbContextFactory<mainContext> dbContextFactory,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartWatchers();

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime? nextRunAt = await RunDueTasksAndGetNextRunAtAsync(stoppingToken);

            Task delayTask = nextRunAt.HasValue
                ? Task.Delay(GetDelayUntil(nextRunAt.Value), stoppingToken)
                : Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);

            Task refreshTask = _refreshSignal.WaitAsync(stoppingToken);

            await Task.WhenAny(delayTask, refreshTask);
        }
    }

    private void StartWatchers()
    {
        string connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        _scheduledTaskScheduleWatcher = new SqlWatcher(
            connectionString,
            """
            SELECT COUNT_BIG(*)
            FROM dbo.ScheduledTaskSchedule
            """);

        _scheduledTaskScheduleWatcher.ChangedAsync += OnWatchedTableChangedAsync;
    }

    private Task OnWatchedTableChangedAsync()
    {
        if (_refreshSignal.CurrentCount == 0)
        {
            _refreshSignal.Release();
        }

        return Task.CompletedTask;
    }

    private async Task<DateTime?> RunDueTasksAndGetNextRunAtAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        DateTime now = DateTime.Now;

        List<ScheduledTask> dueTasks = await db.ScheduledTasks
            .Where(x => x.NextRunAt != null && x.NextRunAt <= now)
            .OrderBy(x => x.NextRunAt)
            .ToListAsync(cancellationToken);

        foreach (ScheduledTask scheduledTask in dueTasks)
        {
            switch (scheduledTask.HandlerKey)
            {
                default:
                    break;
            }

            scheduledTask.LastRunAt = now;
            scheduledTask.NextRunAt = await GetNextRunAtAsync(db, scheduledTask.Id, now, cancellationToken);
            scheduledTask.LastModifiedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await db.ScheduledTasks
            .Where(x => x.NextRunAt != null)
            .MinAsync(x => (DateTime?)x.NextRunAt, cancellationToken);
    }

    private async Task<DateTime?> GetNextRunAtAsync(
        mainContext db,
        int scheduledTaskId,
        DateTime fromTime,
        CancellationToken cancellationToken)
    {
        // Placeholder for now.
        // Later this will look at dbo.ScheduledTaskSchedule rows for the task
        // and calculate the earliest next runtime after fromTime.

        await Task.CompletedTask;
        return null;
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