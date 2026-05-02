using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace AutoCAC.Services.RulesEngine;

public sealed class RuleEngineService : IDisposable
{
    private readonly IDbContextFactory<MainContext> _dbContextFactory;
    private readonly SqlWatcher _watcher;
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    private bool _disposed;
    public RuleEngineService(
        IDbContextFactory<MainContext> dbContextFactory,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

        _watcher = new SqlWatcher(
            connectionString,
            """
            SELECT COUNT_BIG(*)
            FROM dbo.RuleEngineSourceState
            """
        );

        _watcher.ChangedAsync += OnWatcherChangedAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await ProcessPendingSourcesAsync(cancellationToken);
    }

    private async Task OnWatcherChangedAsync()
    {
        await ProcessPendingSourcesAsync(CancellationToken.None);
    }

    private async Task ProcessPendingSourcesAsync(CancellationToken cancellationToken)
    {
        if (!await _processingLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var pendingSources = await db.RuleEngineSourceStates
                .Where(x => x.LastProcessedAt == null || x.LastProcessedAt < x.LastImportAt)
                .OrderBy(x => x.TableName)
                .ToListAsync(cancellationToken);

            foreach (var source in pendingSources)
            {
                await MarkProcessingStartedAsync(source.TableName, cancellationToken);

                try
                {
                    switch (source.TableName)
                    {
                        case "UnitDose":
                        case "IV":
                            await ProcessMedicationOrdersAsync(source.TableName, cancellationToken);
                            break;

                        case "LabResult":
                            await ProcessLabResultsAsync(source.TableName, cancellationToken);
                            break;

                        case "Microbio":
                            await ProcessMicrobioAsync(source.TableName, cancellationToken);
                            break;

                        default:
                            Console.WriteLine($"No handler exists for table '{source.TableName}'.");
                            break;
                    }

                    await MarkProcessedAsync(source.TableName, cancellationToken);
                }
                catch
                {
                    await ClearProcessingStartedAsync(source.TableName, cancellationToken);
                    throw;
                }
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task MarkProcessingStartedAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.RuleEngineSourceStates
            .SingleAsync(x => x.TableName == tableName, cancellationToken);

        row.ProcessingStartedAt = DateTime.Now;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkProcessedAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.RuleEngineSourceStates
            .SingleAsync(x => x.TableName == tableName, cancellationToken);

        row.LastProcessedAt = row.LastImportAt;
        row.ProcessingStartedAt = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ClearProcessingStartedAsync(string tableName, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.RuleEngineSourceStates
            .SingleAsync(x => x.TableName == tableName, cancellationToken);

        row.ProcessingStartedAt = null;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<AlertDefEvaluationRequest>> ProcessMedicationOrdersAsync(string tableName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"MedOrder method goes here for {tableName}");
        return new List<AlertDefEvaluationRequest>();
    }

    private async Task<List<AlertDefEvaluationRequest>> ProcessLabResultsAsync(string tableName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"LabResult method goes here for {tableName}");
        return new List<AlertDefEvaluationRequest>();
    }

    private async Task<List<AlertDefEvaluationRequest>> ProcessMicrobioAsync(string tableName, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Microbio method goes here");
        return new List<AlertDefEvaluationRequest>();
    }

    public sealed class AlertDefEvaluationRequest
    {
        public int PatientId { get; set; }
        public int AlertDefId { get; set; }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher.Dispose();
        _processingLock.Dispose();
    }
}