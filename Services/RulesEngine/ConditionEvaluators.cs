using AutoCAC.Models;
using Microsoft.EntityFrameworkCore;

namespace AutoCAC.Services.RulesEngine;

public sealed class ConditionEvaluators
{
    public IReadOnlyDictionary<string, IConditionEvaluator> Map { get; }

    public ConditionEvaluators(IDbContextFactory<mainContext> dbContextFactory)
    {
        Map = new Dictionary<string, IConditionEvaluator>(StringComparer.OrdinalIgnoreCase)
        {
            ["MedicationOrder"] = new MedicationOrderConditionEvaluator(dbContextFactory)
            //["Microbio"] = new MicrobioConditionEvaluator(dbContextFactory)
        };
    }
}

