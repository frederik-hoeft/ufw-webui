namespace Ufw.Shared.Firewall;

public sealed record FirewallConfigurationSnapshot(
    bool IPv6Enabled,
    FirewallDefaultPolicy IncomingPolicy,
    FirewallDefaultPolicy OutgoingPolicy,
    FirewallDefaultPolicy RoutedPolicy);
