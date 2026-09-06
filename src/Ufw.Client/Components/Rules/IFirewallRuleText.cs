using Ufw.Shared.Firewall;

namespace Ufw.Client.Components.Rules;

internal interface IFirewallRuleText
{
    string DescribeEndpoint(string? address, string? ports, string? networkInterface);

    string FormatAction(FirewallAction action);

    string FormatDirection(FirewallDirection direction);

    string FormatAddressFamily(FirewallAddressFamily family);

    string FormatProtocol(FirewallProtocol protocol);
}
