using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Interop.Output.Model;
using SharedFirewallAction = Ufw.Ipc.Shared.Model.Domain.Rules.FirewallAction;

namespace Ufw.Systemd.Firewall;

internal static class UfwRuleMapper
{
    public static ListedFirewallRule ToListedRule(ObservedUfwRule observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        if (observed.Parsed is null)
        {
            return new ListedFirewallRule
            {
                RuleId = null,
                DisplayNumber = observed.DisplayNumber,
                Parsed = false,
                RawLine = observed.RawLine,
                Rule = null,
            };
        }

        FirewallRuleSpecification specification = ToSpecification(observed.Parsed);
        return new ListedFirewallRule
        {
            RuleId = RuleIdentity.Compute(specification),
            DisplayNumber = observed.DisplayNumber,
            Parsed = true,
            RawLine = observed.RawLine,
            Rule = specification,
        };
    }

    public static FirewallRuleSpecification ToSpecification(UfwListCommandResultRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return RuleSpecificationNormalizer.Normalize(new FirewallRuleSpecification
        {
            Action = row.Type switch
            {
                RuleType.Allow => SharedFirewallAction.Allow,
                RuleType.Deny => SharedFirewallAction.Deny,
                RuleType.Reject => SharedFirewallAction.Reject,
                RuleType.Limit => SharedFirewallAction.Limit,
                _ => throw new InvalidOperationException($"Unsupported UFW rule type '{row.Type}'.")
            },
            Direction = row.Direction switch
            {
                Direction.In => FirewallDirection.In,
                Direction.Out => FirewallDirection.Out,
                Direction.Forward => FirewallDirection.Forward,
                _ => throw new InvalidOperationException($"Unsupported UFW direction '{row.Direction}'.")
            },
            Protocol = row.Protocol switch
            {
                UfwProtocol.Tcp => FirewallProtocol.Tcp,
                UfwProtocol.Udp => FirewallProtocol.Udp,
                _ => FirewallProtocol.Any
            },
            Source = row.Source,
            SourcePorts = row.SourcePorts,
            SourceInterface = row.SourceInterface,
            Destination = row.Destination,
            DestinationPorts = row.DestinationPorts,
            DestinationInterface = row.DestinationInterface,
            Comment = row.Comment ?? row.Context?.Comment,
        });
    }
}
