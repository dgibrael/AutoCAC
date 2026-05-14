using AutoCAC.Common.Alerts;
using AutoCAC.Models;
using System.Text.Json;
using static AutoCAC.Services.RulesEngine.RuleEngineService;

namespace AutoCAC.Services.RulesEngine;

public sealed class AlertDefEvaluator
{
    private readonly List<AlertDefRuleNode> _nodes;
    private readonly List<ClinicalFact> _facts;
    private readonly ILookup<int, AlertDefRuleNode> _childNodesByParentId;
    private readonly ILookup<int, ClinicalFact> _primaryNodeMatches;

    public AlertDefEvaluator(
        List<AlertDefRuleNode> nodes,
        List<ClinicalFact> facts,
        List<PrimaryNodeMatch> primaryMatches)
    {
        _nodes = nodes;
        _facts = facts;

        _childNodesByParentId = _nodes
            .Where(x => x.ParentId != null)
            .ToLookup(x => x.ParentId!.Value);

        _primaryNodeMatches = primaryMatches
            .ToLookup(x => x.RuleNode.Id, x => x.Fact);
    }

    public AlertEvaluationResult Evaluate(int patientId, int alertDefId)
    {
        var rootNodes = _nodes
            .Where(x => x.ParentId == null)
            .ToList();

        var rootResults = rootNodes
            .Select(EvaluateNode)
            .ToList();

        bool isMatch = rootResults.All(x => x.IsMatch);

        var factIds = rootResults
            .Where(x => x.IsMatch)
            .SelectMany(x => x.FactIds)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        return new AlertEvaluationResult
        {
            PatientId = patientId,
            AlertDefId = alertDefId,
            IsMatch = isMatch,
            EvidenceKey = isMatch && factIds.Count > 0
                ? string.Join("|", factIds)
                : null
        };
    }

    private NodeEvaluationResult EvaluateNode(AlertDefRuleNode node)
    {
        switch (node.NodeType)
        {
            case nameof(AlertNodeTypeEnum.Group):
                return EvaluateGroupNode(node);

            case nameof(AlertNodeTypeEnum.Exists):
            case nameof(AlertNodeTypeEnum.ClinicalDefinition):
                return EvaluatePrimaryNode(node);

            case nameof(AlertNodeTypeEnum.Modifier):
                throw new NotSupportedException("Modifier nodes are only valid as children of primary nodes.");

            default:
                throw new NotSupportedException($"Unsupported node type: {node.NodeType}");
        }
    }

    private NodeEvaluationResult EvaluateGroupNode(AlertDefRuleNode node)
    {
        var childNodes = _childNodesByParentId[node.Id].ToList();

        if (childNodes.Count == 0)
            return new NodeEvaluationResult { IsMatch = true };

        var childResults = childNodes
            .Select(EvaluateNode)
            .ToList();

        bool isMatch = node.ChildOperatorEnum switch
        {
            RuleNodeChildOperatorEnum.All => childResults.All(x => x.IsMatch),
            RuleNodeChildOperatorEnum.Any => childResults.Any(x => x.IsMatch),
            RuleNodeChildOperatorEnum.None => !childResults.Any(x => x.IsMatch),
            RuleNodeChildOperatorEnum.NotAll => !childResults.All(x => x.IsMatch),
            RuleNodeChildOperatorEnum.AtLeast => childResults.Count(x => x.IsMatch) >= int.Parse(node.Value),
            RuleNodeChildOperatorEnum.NoMoreThan => childResults.Count(x => x.IsMatch) <= int.Parse(node.Value),
            _ => throw new NotSupportedException($"Unsupported child operator: {node.ChildOperator}")
        };

        var factIds = childResults
            .Where(x => x.IsMatch)
            .SelectMany(x => x.FactIds)
            .ToHashSet();

        return new NodeEvaluationResult
        {
            IsMatch = isMatch,
            FactIds = factIds
        };
    }

    private NodeEvaluationResult EvaluatePrimaryNode(AlertDefRuleNode node)
    {
        var matchingFacts = _primaryNodeMatches[node.Id].ToList();

        if (matchingFacts.Count == 0)
            return new NodeEvaluationResult();

        var childNodes = _childNodesByParentId[node.Id].ToList();

        if (childNodes.Count == 0)
        {
            return new NodeEvaluationResult
            {
                IsMatch = true,
                FactIds = matchingFacts.Select(x => x.Id).ToHashSet()
            };
        }

        if (childNodes.Any(x => x.NodeType != nameof(AlertNodeTypeEnum.Modifier)))
            throw new NotSupportedException("Only Modifier nodes are supported as children of primary nodes.");

        foreach (var fact in matchingFacts)
        {
            if (EvaluateModifierChildren(node, fact, childNodes))
            {
                return new NodeEvaluationResult
                {
                    IsMatch = true,
                    FactIds = new HashSet<long> { fact.Id }
                };
            }
        }

        return new NodeEvaluationResult();
    }

    private bool EvaluateModifierChildren(
        AlertDefRuleNode parentNode,
        ClinicalFact parentFact,
        List<AlertDefRuleNode> childNodes)
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

            default:
                throw new NotSupportedException($"Unsupported child operator: {parentNode.ChildOperator}");
        }
    }

    private bool EvaluateModifierNode(AlertDefRuleNode node, ClinicalFact parentFact)
    {
        if (node.NodeType != nameof(AlertNodeTypeEnum.Modifier))
            throw new NotSupportedException($"Expected Modifier node, got {node.NodeType}");

        if (string.IsNullOrWhiteSpace(node.FieldName))
            throw new InvalidOperationException($"Modifier node {node.Id} is missing FieldName.");

        if (string.IsNullOrWhiteSpace(parentFact.ValuesJson))
            return false;

        using var document = JsonDocument.Parse(parentFact.ValuesJson);

        if (!document.RootElement.TryGetProperty(node.FieldName, out var jsonValue))
            return false;

        return node.ValueMatch(jsonValue.ToString());
    }

    private sealed class NodeEvaluationResult
    {
        public bool IsMatch { get; set; }
        public HashSet<long> FactIds { get; set; } = new();
    }
}