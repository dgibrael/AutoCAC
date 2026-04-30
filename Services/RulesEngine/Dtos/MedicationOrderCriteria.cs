namespace AutoCAC.Services.RulesEngine;

public sealed class MedicationOrderCriteria
{
    public List<int> OrderableItemIds { get; set; } = new();
    public List<string> DrugClasses { get; set; } = new();
}
