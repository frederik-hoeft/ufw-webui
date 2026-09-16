using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering;

internal sealed class PortRuleFilterEvaluator : RuleFilterEvaluator<PortRuleFilter>
{
    protected override RuleMatchEvaluation Evaluate(RuleRowProjection row, PortRuleFilter filter, RuleFilterContext context)
    {
        _ = context;
        FirewallRuleSpecification? rule = row.Rule.Rule;
        if (!row.Rule.Parsed || rule is null)
        {
            return RuleMatchEvaluation.NoMatch;
        }

        List<RuleMatchEvidence> evidence = [];
        if (filter.Endpoint is RuleEndpointField.Any or RuleEndpointField.Source)
        {
            AddEndpointMatch(RuleEndpointField.Source, rule.SourcePorts, filter.Ports, evidence);
        }
        if (filter.Endpoint is RuleEndpointField.Any or RuleEndpointField.Destination)
        {
            AddEndpointMatch(RuleEndpointField.Destination, rule.DestinationPorts, filter.Ports, evidence);
        }
        return evidence.Count == 0 ? RuleMatchEvaluation.NoMatch : new RuleMatchEvaluation(true, evidence);
    }

    private static void AddEndpointMatch(RuleEndpointField endpoint, string? value, RulePortSet query, List<RuleMatchEvidence> evidence)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            evidence.Add(new PortRuleMatchEvidence(endpoint, "any", query.CanonicalValue));
            return;
        }
        if (RulePortSet.TryParse(value, out RulePortSet? rulePorts) && rulePorts is not null && rulePorts.Overlaps(query))
        {
            evidence.Add(new PortRuleMatchEvidence(endpoint, rulePorts.CanonicalValue, query.CanonicalValue));
        }
    }
}
