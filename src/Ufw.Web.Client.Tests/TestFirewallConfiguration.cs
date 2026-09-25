using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Tests;

internal static class TestFirewallConfiguration
{
    public static FirewallConfigurationSnapshot Enabled { get; } = new(
        IPv6Enabled: true,
        IncomingPolicy: FirewallDefaultPolicy.Deny,
        OutgoingPolicy: FirewallDefaultPolicy.Allow,
        RoutedPolicy: FirewallDefaultPolicy.Deny);

    public static FirewallConfigurationSnapshot Disabled { get; } = Enabled with { IPv6Enabled = false };
}
