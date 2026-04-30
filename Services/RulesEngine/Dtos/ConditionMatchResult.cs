namespace AutoCAC.Services.RulesEngine;

public sealed class ConditionMatchResult
{
    public int ConditionDefId { get; set; }
    public int PatientId { get; set; }
    public string TableName { get; set; }
    public int SourceId { get; set; }
}
