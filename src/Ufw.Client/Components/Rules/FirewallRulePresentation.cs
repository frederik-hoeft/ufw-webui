using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Client.Components.Rules;

internal static class FirewallRulePresentation
{
    public static string DescribeRule(ListedFirewallRule listedRule)
    {
        ArgumentNullException.ThrowIfNull(listedRule);
        if (listedRule.Rule is null)
        {
            return listedRule.RawLine;
        }

        FirewallRuleSpecification rule = listedRule.Rule;
        string source = DescribeEndpoint(rule.Source, rule.SourcePorts, rule.SourceInterface);
        string destination = DescribeEndpoint(rule.Destination, rule.DestinationPorts, rule.DestinationInterface);
        return $"{FormatEnum(rule.Action)} {FormatEnum(rule.Direction)} {FormatEnum(rule.Protocol)} from {source} to {destination}";
    }

    public static string DescribeEndpoint(string? address, string? ports, string? networkInterface)
    {
        string value = string.IsNullOrWhiteSpace(address) ? RuleSpecificationNormalizer.ANY : address;
        if (!string.IsNullOrWhiteSpace(ports))
        {
            value += $":{ports}";
        }
        if (!string.IsNullOrWhiteSpace(networkInterface))
        {
            value += $" via {networkInterface}";
        }
        return value;
    }

    public static string? DescribePorts(FirewallRuleSpecification rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        bool hasSource = !string.IsNullOrWhiteSpace(rule.SourcePorts);
        bool hasDestination = !string.IsNullOrWhiteSpace(rule.DestinationPorts);
        return (hasSource, hasDestination) switch
        {
            (false, false) => null,
            (false, true) => rule.DestinationPorts,
            (true, false) => $"src {rule.SourcePorts}",
            (true, true) => $"src {rule.SourcePorts} / dst {rule.DestinationPorts}",
        };
    }

    public static string ActionClass(FirewallAction action) => action switch
    {
        FirewallAction.Allow => "rule-action rule-action-allow",
        FirewallAction.Deny => "rule-action rule-action-deny",
        FirewallAction.Reject => "rule-action rule-action-reject",
        FirewallAction.Limit => "rule-action rule-action-limit",
        _ => "rule-action",
    };

    public static string FormatAddressFamily(FirewallAddressFamily family) => family switch
    {
        FirewallAddressFamily.IPv4 => "IPv4",
        FirewallAddressFamily.IPv6 => "IPv6",
        _ => "Any family",
    };

    public static string FormatProtocol(FirewallProtocol protocol) => protocol switch
    {
        FirewallProtocol.Any => "any",
        FirewallProtocol.Tcp => "tcp",
        FirewallProtocol.Udp => "udp",
        _ => protocol.ToString(),
    };

    public static string FormatEnum<T>(T value) where T : struct, Enum => value.ToString();
}
