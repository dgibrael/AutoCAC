using AutoCAC.Common.Alerts;
using AutoCAC.Models;

namespace AutoCAC.Services.RulesEngine;

public sealed class AlertDefEvaluator
{
    private readonly List<AlertDefRuleNode> _nodes;
    private readonly List<RuleEngineFact> _facts;
    private readonly ILookup<int, AlertDefRuleNode> _childNodesByParentId;
    private readonly ILookup<int, RuleEngineFact> _primaryNodeMatches;

    public AlertDefEvaluator(List<AlertDefRuleNode> nodes, List<RuleEngineFact> facts)
    {
        _nodes = nodes;
        _facts = facts;

        var groupType = AlertDataTypeEnum.Group.ToString();
        var modifierType = AlertDataTypeEnum.Modifier.ToString();

        _childNodesByParentId = _nodes
            .Where(x => x.ParentId != null)
            .ToLookup(x => x.ParentId!.Value);

        _primaryNodeMatches = _nodes
            .Where(x => x.DataType != groupType && x.DataType != modifierType)
            .SelectMany(node => _facts
                .Where(fact => fact.IsActive)
                .Where(fact => fact.DataType == node.DataType)
                .Where(fact => fact.FieldName == node.FieldName)
                .Where(fact => node.ValueMatch(fact.FieldValue))
                .Select(fact => new
                {
                    NodeId = node.Id,
                    Fact = fact
                }))
            .ToLookup(x => x.NodeId, x => x.Fact);
    }

    public bool Evaluate()
    {
        var rootNodes = _nodes
            .Where(x => x.ParentId == null)
            .ToList();

        return rootNodes.All(EvaluateNode);
    }

    private bool EvaluateNode(AlertDefRuleNode node)
    {
        if (node.DataType == AlertDataTypeEnum.Group.ToString())
            return EvaluateGroupNode(node);

        return EvaluatePrimaryNode(node);
    }

    private bool EvaluateGroupNode(AlertDefRuleNode node)
    {
        var childNodes = _childNodesByParentId[node.Id];

        if (!childNodes.Any())
            return true;

        switch (node.ChildOperatorEnum)
        {
            case RuleNodeChildOperatorEnum.All:
                return childNodes.All(EvaluateNode);

            case RuleNodeChildOperatorEnum.Any:
                return childNodes.Any(EvaluateNode);

            case RuleNodeChildOperatorEnum.None:
                return !childNodes.Any(EvaluateNode);

            case RuleNodeChildOperatorEnum.NotAll:
                return !childNodes.All(EvaluateNode);

            case RuleNodeChildOperatorEnum.AtLeast:
                return childNodes.Count(EvaluateNode) >= int.Parse(node.Value);

            case RuleNodeChildOperatorEnum.NoMoreThan:
                return childNodes.Count(EvaluateNode) <= int.Parse(node.Value);
        }

        throw new NotSupportedException($"Unsupported operator for Group node: {node.ChildOperator}");
    }

    private bool EvaluatePrimaryNode(AlertDefRuleNode node)
    {
        var matchingFacts = _primaryNodeMatches[node.Id];

        if (!matchingFacts.Any())
            return false;

        var childNodes = _childNodesByParentId[node.Id];

        if (!childNodes.Any())
            return true;

        if (childNodes.Any(x => x.DataType == AlertDataTypeEnum.Group.ToString()))
            throw new NotSupportedException("Nested Group nodes under primary nodes are not supported.");

        foreach (var fact in matchingFacts)
        {
            if (EvaluatePrimaryNodeChildren(node, fact, childNodes))
                return true;
        }

        return false;
    }

    private bool EvaluatePrimaryNodeChildren(
        AlertDefRuleNode parentNode,
        RuleEngineFact parentFact,
        IEnumerable<AlertDefRuleNode> childNodes)
    {
        switch (parentNode.ChildOperatorEnum)
        {
            case RuleNodeChildOperatorEnum.All:
                return childNodes.All(child => EvaluateModifierNode(child, parentFact));

            case RuleNodeChildOperatorEnum.Any:
                return childNodes.Any(child => EvaluateModifierNode(child, parentFact));

            case RuleNodeChildOperatorEnum.None:
                return !childNodes.Any(child => EvaluateModifierNode(child, parentFact));

            case RuleNodeChildOperatorEnum.NotAll:
                return !childNodes.All(child => EvaluateModifierNode(child, parentFact));

            case RuleNodeChildOperatorEnum.AtLeast:
                return childNodes.Count(child => EvaluateModifierNode(child, parentFact)) >= int.Parse(parentNode.Value);

            case RuleNodeChildOperatorEnum.NoMoreThan:
                return childNodes.Count(child => EvaluateModifierNode(child, parentFact)) <= int.Parse(parentNode.Value);
        }

        throw new NotSupportedException($"Unsupported child operator: {parentNode.ChildOperator}");
    }

    private bool EvaluateModifierNode(AlertDefRuleNode node, RuleEngineFact parentFact)
    {
        if (node.DataType != AlertDataTypeEnum.Modifier.ToString())
            throw new NotSupportedException($"Expected Modifier node, got {node.DataType}");

        return _facts
            .Where(x => x.IsActive)
            .Where(x => x.DataType == parentFact.DataType)
            .Where(x => x.RecordKey == parentFact.RecordKey)
            .Where(x => x.FieldName == node.FieldName)
            .Any(x => node.ValueMatch(x.FieldValue));
    }
}