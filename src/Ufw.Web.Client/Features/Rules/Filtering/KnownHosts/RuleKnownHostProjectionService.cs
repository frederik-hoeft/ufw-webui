using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Features.Rules.Filtering.Networks;
using Ufw.Web.Client.Features.Rules.Filtering.Semantics;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;

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

        NetworkFilterOperand? source = ParseEndpoint(rule.Source, context.AddressFamily);
        NetworkFilterOperand? destination = ParseEndpoint(rule.Destination, context.AddressFamily);
        if (source is null && destination is null)
        {
            return [];
        }

        List<RuleKnownHostProjection> projections = [];
        foreach (KnownHostInventoryItem host in context.KnownHosts)
        {
            if (!host.IsVisible
                || host.AddressFamily != context.AddressFamily
                || !RuleFilterSemantics.TryParseNetwork(host.Address, out NetworkFilterOperand? hostNetwork)
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

    private static bool Overlaps(NetworkFilterOperand? ruleNetwork, NetworkFilterOperand hostNetwork) =>
        ruleNetwork is not null && ruleNetwork.Overlaps(hostNetwork);

    private static NetworkFilterOperand? ParseEndpoint(string? value, FirewallAddressFamily addressFamily)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, RuleSpecificationNormalizer.ANY, StringComparison.OrdinalIgnoreCase))
        {
            return RuleFilterSemantics.AnyNetwork(addressFamily);
        }

        return RuleFilterSemantics.TryParseNetwork(value, out NetworkFilterOperand? network)
            && network is not null
            && network.AddressFamily == addressFamily
            ? network
            : null;
    }
}
