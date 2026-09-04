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

    public static string FormatEnum<T>(T value) where T : struct, Enum => value.ToString();
}
