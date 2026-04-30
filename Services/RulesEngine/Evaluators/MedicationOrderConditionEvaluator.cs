using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCAC.Services.RulesEngine;

public sealed class MedicationOrderConditionEvaluator
    : ConditionEvaluatorBase<MedicationOrderCriteria>
{
    public MedicationOrderConditionEvaluator(IDbContextFactory<mainContext> dbContextFactory)
        : base(dbContextFactory)
    {
    }

    protected override async Task<IReadOnlyList<ConditionMatchResult>> EvaluateAsync(
        ConditionDef conditionDef,
        MedicationOrderCriteria criteria,
        int patientId,
        CancellationToken cancellationToken)
    {
        await using var db = await CreateDbContextAsync(cancellationToken);
        var results = new List<ConditionMatchResult>();
        var hasDrugIds = criteria.OrderableItemIds.Count > 0;
        var hasDrugClasses = criteria.DrugClasses.Count > 0;

        //results.AddRange(unitDoseMatches);

        var ivMatches = await db.Ivs
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .Where(x => x.StopDateTime > DateTime.Now)
            .Where(x =>
                MatchMedication(
                    x.PharmacyOrderableItemId ?? 0,
                    x.PharmacyOrderableItem.Drugs.Select(d => d.DrugClass).ToList(),
                    criteria.OrderableItemIds,
                    criteria.DrugClasses,
                    hasDrugIds,
                    hasDrugClasses))
            .Select(x => new ConditionMatchResult
            {
                ConditionDefId = conditionDef.Id,
                PatientId = patientId,
                TableName = "IV",
                SourceId = (int)x.Id
            })
            .ToListAsync(cancellationToken);

        results.AddRange(ivMatches);

        return results;
    }

    private static bool MatchMedication(
        int orderableItemId,
        IEnumerable<string> orderDrugClasses,
        List<int> allowedOrderableItemIds,
        List<string> allowedDrugClasses,
        bool hasOrderableItemIds,
        bool hasDrugClasses)
    {
        var orderableItemMatched =
            hasOrderableItemIds &&
            allowedOrderableItemIds.Contains(orderableItemId);

        var drugClassMatched =
            hasDrugClasses &&
            orderDrugClasses.Any(orderDrugClass =>
                allowedDrugClasses.Contains(orderDrugClass, StringComparer.OrdinalIgnoreCase));

        return orderableItemMatched || drugClassMatched;
    }
}