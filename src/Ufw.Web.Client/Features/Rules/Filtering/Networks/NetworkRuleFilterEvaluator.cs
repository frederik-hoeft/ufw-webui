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

    private static void AddEndpointMatch(RuleEndpointField endpoint, string? value, RuleNetwork query, RuleFilterContext context, List<RuleMatchEvidence> evidence)
    {
        RuleNetwork? ruleNetwork;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            ruleNetwork = RuleNetwork.Any(context.AddressFamily);
        }
        else if (!RuleNetwork.TryParse(value, out RuleNetwork? parsedRuleNetwork) || parsedRuleNetwork is null || parsedRuleNetwork.AddressFamily != context.AddressFamily)
        {
            return;
        }
        else
        {
            ruleNetwork = parsedRuleNetwork;
        }

        NetworkRuleMatchEvidence.RelationshipKind relationship;
        if (ruleNetwork.PrefixLength == query.PrefixLength && ruleNetwork.NetworkAddress.Equals(query.NetworkAddress))
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
