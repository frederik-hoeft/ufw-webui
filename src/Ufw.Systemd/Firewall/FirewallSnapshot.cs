using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall;

/// <summary>Authoritative observed UFW state used inside daemon firewall operations.</summary>
internal sealed record FirewallSnapshot(bool Active, IReadOnlyList<ListedFirewallRule> Rules, FirewallConfigurationSnapshot Configuration)
{
    public FirewallStateAssessment Assessment { get; init; } = FirewallStateAssessment.Clean;
}
