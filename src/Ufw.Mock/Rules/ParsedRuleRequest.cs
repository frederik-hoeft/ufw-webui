using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Mock.State;

namespace Ufw.Mock.Rules;

internal sealed class ParsedRuleRequest
{
    public required UfwMockRule Rule { get; init; }

    public IReadOnlyList<UfwMockRule> Materialize(bool ipv6Enabled)
    {
        FirewallAddressFamily family = Rule.Specification.AddressFamily;
        if (family != FirewallAddressFamily.Any)
        {
            return [CloneForFamily(Rule, family)];
        }

        if (string.Equals(Rule.ExtendedProtocol, "ipv6", StringComparison.Ordinal)
            || string.Equals(Rule.ExtendedProtocol, "igmp", StringComparison.Ordinal))
        {
            return [CloneForFamily(Rule, FirewallAddressFamily.IPv4)];
        }

        return ipv6Enabled
            ? [CloneForFamily(Rule, FirewallAddressFamily.IPv4), CloneForFamily(Rule, FirewallAddressFamily.IPv6)]
            : [CloneForFamily(Rule, FirewallAddressFamily.IPv4)];
    }

    private static UfwMockRule CloneForFamily(UfwMockRule rule, FirewallAddressFamily family)
    {
        FirewallRuleSpecification specification = rule.Specification;
        return new UfwMockRule
        {
            Specification = new FirewallRuleSpecification
            {
                Action = specification.Action,
                AddressFamily = family,
                Direction = specification.Direction,
                Protocol = specification.Protocol,
                Source = specification.Source,
                SourcePorts = specification.SourcePorts,
                SourceInterface = specification.SourceInterface,
                Destination = specification.Destination,
                DestinationPorts = specification.DestinationPorts,
                DestinationInterface = specification.DestinationInterface,
                Comment = specification.Comment,
            },
            ExtendedProtocol = rule.ExtendedProtocol,
            Logging = rule.Logging,
            SourceApplicationName = rule.SourceApplicationName,
            DestinationApplicationName = rule.DestinationApplicationName,
        };
    }
}
