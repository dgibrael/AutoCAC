namespace AutoCAC.Common.Alerts;

public enum OriginTypeEnum
{
    Scheduled,
    RulesEngine,
    Manual
}

public enum DataTypeEnum
{
    LabResult,
    MicrobioResult,
    MedOrder,
    Diagnosis
}

public enum PredicateOperatorEnum
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    NotContains,
    Null,
    NotNull
}