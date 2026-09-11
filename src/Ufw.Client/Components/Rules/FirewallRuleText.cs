using Microsoft.Extensions.Localization;
using Ufw.Client.Localization;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

internal sealed class FirewallRuleText(IStringLocalizer<RulesStrings> rulesText) : IFirewallRuleText
{
    public string DescribeEndpoint(string? address, string? ports, string? networkInterface)
    {
        string value = string.IsNullOrWhiteSpace(address) ? RuleSpecificationNormalizer.ANY : address;
        if (!string.IsNullOrWhiteSpace(ports))
        {
            string displayPorts = ports.Replace(':', '-');
            value = rulesText["EndpointPort", value, displayPorts];
        }
        if (!string.IsNullOrWhiteSpace(networkInterface))
        {
            value = rulesText["EndpointVia", value, networkInterface];
        }
        return value;
    }

    public string FormatAction(FirewallAction action) => action switch
    {
        FirewallAction.Allow => rulesText["Allow"],
        FirewallAction.Deny => rulesText["Deny"],
        FirewallAction.Reject => rulesText["Reject"],
        FirewallAction.Limit => rulesText["Limit"],
        _ => action.ToString(),
    };

    public string FormatDirection(FirewallDirection direction) => direction switch
    {
        FirewallDirection.In => rulesText["In"],
        FirewallDirection.Out => rulesText["Out"],
        FirewallDirection.Forward => rulesText["Forward"],
        _ => direction.ToString(),
    };

    public string FormatAddressFamily(FirewallAddressFamily family) => family switch
    {
        FirewallAddressFamily.IPv4 => "IPv4",
        FirewallAddressFamily.IPv6 => "IPv6",
        _ => rulesText["AnyFamily"],
    };

    public string FormatProtocol(FirewallProtocol protocol) => protocol switch
    {
        FirewallProtocol.Any => rulesText["AnyProtocol"],
        FirewallProtocol.Tcp => "TCP",
        FirewallProtocol.Udp => "UDP",
        _ => protocol.ToString(),
    };
}
