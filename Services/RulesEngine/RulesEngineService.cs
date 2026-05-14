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

            var existsType = AlertNodeTypeEnum.Exists.ToString();
            var clinicalDefinitionType = AlertNodeTypeEnum.ClinicalDefinition.ToString();

            var existsPrimaryMatches = await (
                from fact in db.ClinicalFacts.AsNoTracking()
                from node in db.AlertDefRuleNodes.AsNoTracking()
                where node.IsActive
                   && node.NodeType == existsType
                   && node.Value == fact.DataType
                   && (fact.NeedsProcessing || (fact.IsActive
                   && (
                        from fact2 in db.ClinicalFacts.AsNoTracking()
                        from node2 in db.AlertDefRuleNodes.AsNoTracking()
                        where fact2.NeedsProcessing
                           && node2.IsActive
                           && node2.NodeType == existsType
                           && node2.Value == fact2.DataType
                           && fact2.PatientId == fact.PatientId
                           && node2.AlertDefId == node.AlertDefId
                        select 1
                      ).Any()))
                select new PrimaryNodeMatch
                {
                    Fact = fact,
                    RuleNode = node
                })
                .ToListAsync(cancellationToken);

            var clinicalDefinitionPrimaryMatches = await (
                from fact in db.ClinicalFacts.AsNoTracking()
                join metaMatch in db.ClinicalDefinitionMetadataMatches.AsNoTracking()
                    on new { fact.DataType, fact.MetadataRecordId }
                    equals new { metaMatch.DataType, metaMatch.MetadataRecordId }
                join node in db.AlertDefRuleNodes.AsNoTracking()
                    on metaMatch.ClinicalDefinitionId equals node.ClinicalDefinitionId
                where node.IsActive
                   && node.NodeType == clinicalDefinitionType
                   && (fact.NeedsProcessing || 
                        (fact.IsActive && (
                        from fact2 in db.ClinicalFacts.AsNoTracking()
                        join metaMatch2 in db.ClinicalDefinitionMetadataMatches.AsNoTracking()
                            on new { fact2.DataType, fact2.MetadataRecordId }
                            equals new { metaMatch2.DataType, metaMatch2.MetadataRecordId }
                        join node2 in db.AlertDefRuleNodes.AsNoTracking()
                            on metaMatch2.ClinicalDefinitionId equals node2.ClinicalDefinitionId
                        where fact2.NeedsProcessing
                           && node2.IsActive
                           && node2.NodeType == clinicalDefinitionType
                           && fact2.PatientId == fact.PatientId
                           && node2.AlertDefId == node.AlertDefId
                        select 1
                      ).Any()))
                select new PrimaryNodeMatch
                {
                    Fact = fact,
                    RuleNode = node
                })
                .ToListAsync(cancellationToken);

            var allPrimaryMatches = existsPrimaryMatches
                .Concat(clinicalDefinitionPrimaryMatches)
                .GroupBy(x => new { ClinicalFactId = x.Fact.Id, AlertDefRuleNodeId = x.RuleNode.Id })
                .Select(x => x.First())
                .ToList();

            var candidatePairs = allPrimaryMatches
                .Select(x => new
                {
                    x.Fact.PatientId,
                    x.RuleNode.AlertDefId
                })
                .Distinct()
                .ToList();

            if (candidatePairs.Count == 0)
                return;

            var alertDefIds = candidatePairs
                .Select(x => x.AlertDefId)
                .Distinct()
                .ToHashSet();

            var allRuleNodes = await db.AlertDefRuleNodes
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Where(x => alertDefIds.Contains(x.AlertDefId))
                .ToListAsync(cancellationToken);

            var results = new List<AlertEvaluationResult>();

            foreach (var pair in candidatePairs)
            {
                var patientFacts = allPrimaryMatches
                    .Where(x => x.Fact.PatientId == pair.PatientId && x.RuleNode.AlertDefId == pair.AlertDefId)
                    .Select(x => x.Fact)
                    .DistinctBy(x => x.Id)
                    .ToList();

                var alertRuleNodes = allRuleNodes
                    .Where(x => x.AlertDefId == pair.AlertDefId)
                    .ToList();

                var primaryMatches = allPrimaryMatches
                    .Where(x => x.Fact.PatientId == pair.PatientId && x.RuleNode.AlertDefId == pair.AlertDefId)
                    .ToList();

                var evaluator = new AlertDefEvaluator(alertRuleNodes, patientFacts, primaryMatches);
                results.Add(evaluator.Evaluate(pair.PatientId, pair.AlertDefId));
            }

            // next step:
            // apply alert create/update/deactivate logic from results
            // then clear NeedsProcessing on the relevant ClinicalFacts
            // then update ProcessState.ProcessingStartedAt if needed
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
        public string EvidenceKey { get; set; }
        public bool IsMatch { get; set; }
    }

    public sealed class PrimaryNodeMatch
    {
        public ClinicalFact Fact { get; set; }
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