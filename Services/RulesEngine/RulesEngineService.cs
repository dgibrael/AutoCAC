using AutoCAC.Common.Alerts;
using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace AutoCAC.Services.RulesEngine;

public sealed class RuleEngineService : IDisposable
{
    private readonly IDbContextFactory<MainContext> _dbContextFactory;
    private readonly SqlWatcher _watcher;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    private List<AlertDefRuleNode> _entryRuleNodes = new();
    private bool _disposed;
    public RuleEngineService(
        IDbContextFactory<MainContext> dbContextFactory,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        _watcher = new SqlWatcher(
            connectionString,
            """
            SELECT COUNT_BIG(*)
            FROM dbo.ProcessState
            WHERE ProcessName = 'RuleEngineImport'
            and (ProcessingStartedAt IS NULL OR LastImportCompletedAt > ProcessingStartedAt)
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

            var groupType = AlertDataTypeEnum.Group.ToString();
            var modifierType = AlertDataTypeEnum.Modifier.ToString();

            var pendingFacts = await (
                from fact in db.RuleEngineFacts.AsNoTracking()
                from node in db.AlertDefRuleNodes
                                .Include(x => x.AlertDef)
                                .AsNoTracking()
                where fact.IsActive
                   && fact.NeedsProcessing
                   && node.IsActive
                   && node.DataType != groupType
                   && node.DataType != modifierType
                   && node.DataType == fact.DataType
                   && node.FieldName == fact.FieldName
                select new PendingRuleMatch
                {
                    Fact = fact,
                    RuleNode = node
                })
                .ToListAsync(cancellationToken);
            pendingFacts = pendingFacts
                .Where(x => x.RuleNode.ValueMatch(x.Fact.FieldValue))
                .ToList();
            var candidatePairs = pendingFacts
                .Select(x => new
                {
                    PatientId = x.Fact.PatientId!.Value,
                    x.RuleNode.AlertDefId
                })
                .Distinct()
                .ToList();

            var patientIds = candidatePairs
                .Select(x => x.PatientId)
                .Distinct()
                .ToHashSet();

            var alertDefIds = candidatePairs
                .Select(x => x.AlertDefId)
                .Distinct()
                .ToHashSet();

            var allFacts = await db.RuleEngineFacts
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => x.PatientId != null && patientIds.Contains(x.PatientId.Value))
                .ToListAsync(cancellationToken);

            var allRuleNodes = await db.AlertDefRuleNodes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => alertDefIds.Contains(x.AlertDefId))
                .ToListAsync(cancellationToken);
            var results = new List<AlertEvaluationResult>();

            foreach (var pair in candidatePairs)
            {
                var patientFacts = allFacts
                    .Where(x => x.PatientId == pair.PatientId)
                    .ToList();

                var alertRuleNodes = allRuleNodes
                    .Where(x => x.AlertDefId == pair.AlertDefId)
                    .ToList();

                var evaluator = new AlertDefEvaluator(alertRuleNodes, patientFacts);
                bool matched = evaluator.Evaluate();

                results.Add(new AlertEvaluationResult
                {
                    PatientId = pair.PatientId,
                    AlertDefId = pair.AlertDefId,
                    IsMatch = matched
                });
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    public sealed class AlertEvaluationResult
    {
        public int PatientId { get; set; }
        public int AlertDefId { get; set; }
        public bool IsMatch { get; set; }
    }

    public sealed class PendingRuleMatch
    {
        public RuleEngineFact Fact { get; set; }
        public AlertDefRuleNode RuleNode { get; set; }
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