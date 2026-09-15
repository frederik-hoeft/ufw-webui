using Ufw.Shared.Firewall;

namespace Ufw.Client.Rules.Presentation;

internal interface IFirewallRuleText
{
    string DescribeEndpoint(string? address, string? ports, string? networkInterface);

    string FormatAction(FirewallAction action);

    string FormatDirection(FirewallDirection direction);

    string FormatDefaultPolicy(FirewallDefaultPolicy policy);

    string FormatAddressFamily(FirewallAddressFamily family);

    string FormatProtocol(FirewallProtocol protocol);
}
