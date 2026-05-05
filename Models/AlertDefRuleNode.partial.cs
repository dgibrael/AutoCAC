using AutoCAC.Common.Alerts;

namespace AutoCAC.Models;

public partial class AlertDefRuleNode
{
    public PredicateOperatorEnum? OperatorEnum => string.IsNullOrWhiteSpace(Operator) ? null :
        Enum.Parse<PredicateOperatorEnum>(Operator, ignoreCase: true);
    public RuleNodeChildOperatorEnum? ChildOperatorEnum => string.IsNullOrWhiteSpace(ChildOperator) ? RuleNodeChildOperatorEnum.All :
        Enum.Parse<RuleNodeChildOperatorEnum>(ChildOperator, ignoreCase: true);

    public RuleNodeFieldDataTypeEnum FieldDataTypeEnum => string.IsNullOrWhiteSpace(FieldDataType) ? RuleNodeFieldDataTypeEnum.String :
        Enum.Parse<RuleNodeFieldDataTypeEnum>(FieldDataType, ignoreCase: true);

    public bool ValueMatch(string value)
    {
        if (OperatorEnum == null)
            return true;

        var op = OperatorEnum.Value;
        switch (op)
        {
            case PredicateOperatorEnum.Contains:
                return value.Contains(Value, StringComparison.OrdinalIgnoreCase);
            case PredicateOperatorEnum.NotContains:
                return !value.Contains(Value, StringComparison.OrdinalIgnoreCase);
            case PredicateOperatorEnum.StartsWith:
                return value.StartsWith(Value, StringComparison.OrdinalIgnoreCase);
            case PredicateOperatorEnum.NotStartsWith:
                return !value.StartsWith(Value, StringComparison.OrdinalIgnoreCase);
            case PredicateOperatorEnum.EndsWith:
                return value.EndsWith(Value, StringComparison.OrdinalIgnoreCase);
            case PredicateOperatorEnum.NotEndsWith:
                return !value.EndsWith(Value, StringComparison.OrdinalIgnoreCase);
            case PredicateOperatorEnum.Null:
                return string.IsNullOrWhiteSpace(value);

            case PredicateOperatorEnum.NotNull:
                return !string.IsNullOrWhiteSpace(value);

            case PredicateOperatorEnum.Equal:
            case PredicateOperatorEnum.NotEqual:
            case PredicateOperatorEnum.GreaterThan:
            case PredicateOperatorEnum.LessThan:
            case PredicateOperatorEnum.GreaterThanOrEqual:
            case PredicateOperatorEnum.LessThanOrEqual:
                return TypedValueMatch(value);

            default:
                throw new NotSupportedException($"Unsupported operator: {Operator}");
        }
    }

    private bool TypedValueMatch(string value)
    {
        return FieldDataTypeEnum switch
        {
            RuleNodeFieldDataTypeEnum.Int => CompareInts(value),
            RuleNodeFieldDataTypeEnum.Decimal => CompareDecimals(value),
            RuleNodeFieldDataTypeEnum.Bool => CompareBools(value),
            RuleNodeFieldDataTypeEnum.String => CompareStrings(value),
            RuleNodeFieldDataTypeEnum.MinutesAgo or
            RuleNodeFieldDataTypeEnum.HoursAgo or
            RuleNodeFieldDataTypeEnum.DaysAgo => CompareDateTimes(value),
            _ => throw new NotSupportedException($"Unsupported FieldDataType: {FieldDataType}")
        };
    }

    private bool CompareInts(string value)
    {
        int actual = int.Parse(value);
        int expected = int.Parse(Value);

        return OperatorEnum switch
        {
            PredicateOperatorEnum.Equal => actual == expected,
            PredicateOperatorEnum.NotEqual => actual != expected,
            PredicateOperatorEnum.GreaterThan => actual > expected,
            PredicateOperatorEnum.LessThan => actual < expected,
            PredicateOperatorEnum.GreaterThanOrEqual => actual >= expected,
            PredicateOperatorEnum.LessThanOrEqual => actual <= expected,
            _ => throw new NotSupportedException()
        };
    }

    private bool CompareDecimals(string value)
    {
        decimal actual = decimal.Parse(value);
        decimal expected = decimal.Parse(Value);

        return OperatorEnum switch
        {
            PredicateOperatorEnum.Equal => actual == expected,
            PredicateOperatorEnum.NotEqual => actual != expected,
            PredicateOperatorEnum.GreaterThan => actual > expected,
            PredicateOperatorEnum.LessThan => actual < expected,
            PredicateOperatorEnum.GreaterThanOrEqual => actual >= expected,
            PredicateOperatorEnum.LessThanOrEqual => actual <= expected,
            _ => throw new NotSupportedException()
        };
    }

    private bool CompareBools(string value)
    {
        bool actual = bool.Parse(value);
        bool expected = bool.Parse(Value);

        return OperatorEnum switch
        {
            PredicateOperatorEnum.Equal => actual == expected,
            PredicateOperatorEnum.NotEqual => actual != expected,
            _ => throw new NotSupportedException()
        };
    }

    private bool CompareStrings(string value)
    {
        int comparison = string.Compare(value, Value, StringComparison.OrdinalIgnoreCase);

        return OperatorEnum switch
        {
            PredicateOperatorEnum.Equal => comparison == 0,
            PredicateOperatorEnum.NotEqual => comparison != 0,
            PredicateOperatorEnum.GreaterThan => comparison > 0,
            PredicateOperatorEnum.LessThan => comparison < 0,
            PredicateOperatorEnum.GreaterThanOrEqual => comparison >= 0,
            PredicateOperatorEnum.LessThanOrEqual => comparison <= 0,
            _ => throw new NotSupportedException()
        };
    }

    private bool CompareDateTimes(string value)
    {
        DateTime actual = DateTime.Parse(value);
        int amount = -int.Parse(Value);

        DateTime expected = FieldDataTypeEnum switch
        {
            RuleNodeFieldDataTypeEnum.MinutesAgo => DateTime.Now.AddMinutes(amount),
            RuleNodeFieldDataTypeEnum.HoursAgo => DateTime.Now.AddHours(amount),
            RuleNodeFieldDataTypeEnum.DaysAgo => DateTime.Today.AddDays(amount),
            _ => throw new NotSupportedException()
        };

        return OperatorEnum switch
        {
            PredicateOperatorEnum.Equal => actual == expected,
            PredicateOperatorEnum.NotEqual => actual != expected,
            PredicateOperatorEnum.GreaterThan => actual > expected,
            PredicateOperatorEnum.LessThan => actual < expected,
            PredicateOperatorEnum.GreaterThanOrEqual => actual >= expected,
            PredicateOperatorEnum.LessThanOrEqual => actual <= expected,
            _ => throw new NotSupportedException()
        };
    }
}