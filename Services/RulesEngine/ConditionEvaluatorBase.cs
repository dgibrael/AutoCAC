using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace AutoCAC.Services.RulesEngine;

public interface IConditionEvaluator
{
    Task<IReadOnlyList<ConditionMatchResult>> EvaluateAsync(
        ConditionDef conditionDef,
        int patientId,
        CancellationToken cancellationToken);
}

public abstract class ConditionEvaluatorBase<TCriteria> : IConditionEvaluator
{
    private readonly IDbContextFactory<mainContext> _dbContextFactory;

    protected ConditionEvaluatorBase(IDbContextFactory<mainContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    protected Task<mainContext> CreateDbContextAsync(CancellationToken cancellationToken)
    {
        return _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConditionMatchResult>> EvaluateAsync(
        ConditionDef conditionDef,
        int patientId,
        CancellationToken cancellationToken)
    {
        var criteria = JsonSerializer.Deserialize<TCriteria>(conditionDef.CriteriaJson);

        if (criteria == null)
            throw new InvalidOperationException(
                $"Could not deserialize {nameof(conditionDef.CriteriaJson)} for ConditionDef {conditionDef.Id} ({conditionDef.Name}) to {typeof(TCriteria).Name}.");

        return await EvaluateAsync(conditionDef, criteria, patientId, cancellationToken);
    }

    protected abstract Task<IReadOnlyList<ConditionMatchResult>> EvaluateAsync(
        ConditionDef conditionDef,
        TCriteria criteria,
        int patientId,
        CancellationToken cancellationToken);
}