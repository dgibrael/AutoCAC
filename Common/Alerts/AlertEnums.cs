namespace AutoCAC.Common.Alerts;

public enum OriginTypeEnum
{
    Scheduled,
    RulesEngine,
    Manual
}

public enum AlertNodeTypeEnum
{
    Group,
    ClinicalDefinition,
    Modifier,
    Exists
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
    StartsWith,
    NotStartsWith,
    EndsWith,
    NotEndsWith,
    Null,
    NotNull
}

public enum RuleNodeChildOperatorEnum
{
    All,
    Any,
    None,
    NotAll,
    AtLeast,
    NoMoreThan
}

public enum RuleNodeFieldDataTypeEnum
{
    String,
    Int,
    Decimal,
    Bool,
    MinutesAgo,
    HoursAgo,
    DaysAgo
}