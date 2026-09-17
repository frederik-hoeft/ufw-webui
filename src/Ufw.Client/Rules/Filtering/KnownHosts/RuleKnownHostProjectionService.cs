using Ufw.Client.Api;
using Ufw.Client.Rules.Filtering.Networks;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Filtering.KnownHosts;

internal sealed class RuleKnownHostProjectionService : IRuleKnownHostProjectionService
{
    public IReadOnlyList<RuleKnownHostProjection> Project(RuleRowProjection row, RuleFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(context);

        FirewallRuleSpecification? rule = row.Rule.Rule;
        if (!row.Rule.Parsed || rule is null || context.KnownHosts.Count == 0)
        {
            return [];
        }

        RuleNetwork? source = ParseEndpoint(rule.Source, context.AddressFamily);
        RuleNetwork? destination = ParseEndpoint(rule.Destination, context.AddressFamily);
        if (source is null && destination is null)
        {
            return [];
        }

        List<RuleKnownHostProjection> projections = [];
        foreach (KnownHostInventoryItem host in context.KnownHosts)
        {
            if (!host.IsVisible
                || host.AddressFamily != context.AddressFamily
                || !RuleNetwork.TryParse(host.Address, out RuleNetwork? hostNetwork)
                || hostNetwork is null)
            {
                continue;
            }

            if (Overlaps(source, hostNetwork))
            {
                projections.Add(new RuleKnownHostProjection(RuleEndpointField.Source, host));
            }
            if (Overlaps(destination, hostNetwork))
            {
                projections.Add(new RuleKnownHostProjection(RuleEndpointField.Destination, host));
            }
        }

        return projections;
    }

    private static bool Overlaps(RuleNetwork? ruleNetwork, RuleNetwork hostNetwork) =>
        ruleNetwork is not null && (ruleNetwork.Contains(hostNetwork) || hostNetwork.Contains(ruleNetwork));

    private static RuleNetwork? ParseEndpoint(string? value, FirewallAddressFamily addressFamily)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            return RuleNetwork.Any(addressFamily);
        }

        return RuleNetwork.TryParse(value, out RuleNetwork? network)
            && network is not null
            && network.AddressFamily == addressFamily
            ? network
            : null;
    }
}
