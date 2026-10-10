using Ufw.Web.Client.Features.Rules.Filtering.Semantics;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Networks;

internal sealed class NetworkRuleFilterEvaluator : RuleFilterEvaluator<NetworkRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, NetworkRuleFilter filter, RuleFilterContext context)
    {
        FirewallRuleSpecification? rule = row.Rule.Rule;
        if (!row.Rule.Parsed || rule is null || filter.Network.AddressFamily != context.AddressFamily)
        {
            return RuleMatchEvaluation.NoMatch;
        }

        List<RuleMatchEvidence> evidence = [];
        if (filter.Endpoint is RuleEndpointField.Any or RuleEndpointField.Source)
        {
            AddEndpointMatch(RuleEndpointField.Source, rule.Source, filter.Network, context, evidence);
        }
        if (filter.Endpoint is RuleEndpointField.Any or RuleEndpointField.Destination)
        {
            AddEndpointMatch(RuleEndpointField.Destination, rule.Destination, filter.Network, context, evidence);
        }
        return evidence.Count == 0 ? RuleMatchEvaluation.NoMatch : new RuleMatchEvaluation(true, evidence);
    }

    private static void AddEndpointMatch(RuleEndpointField endpoint, string? value, NetworkFilterOperand query, RuleFilterContext context, List<RuleMatchEvidence> evidence)
    {
        NetworkFilterOperand? ruleNetwork;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            ruleNetwork = RuleFilterSemantics.AnyNetwork(context.AddressFamily);
        }
        else if (!RuleFilterSemantics.TryParseNetwork(value, out NetworkFilterOperand? parsedRuleNetwork) || parsedRuleNetwork is null || parsedRuleNetwork.AddressFamily != context.AddressFamily)
        {
            return;
        }
        else
        {
            ruleNetwork = parsedRuleNetwork;
        }

        NetworkRuleMatchEvidence.RelationshipKind relationship;
        if (ruleNetwork.SetEquals(query))
        {
            relationship = NetworkRuleMatchEvidence.RelationshipKind.Equal;
        }
        else if (ruleNetwork.Contains(query))
        {
            relationship = NetworkRuleMatchEvidence.RelationshipKind.ContainsQuery;
        }
        else if (query.Contains(ruleNetwork))
        {
            relationship = NetworkRuleMatchEvidence.RelationshipKind.ContainedByQuery;
        }
        else
        {
            return;
        }

        evidence.Add(new NetworkRuleMatchEvidence(endpoint, ruleNetwork.CanonicalValue, query.CanonicalValue, relationship));
    }
}
