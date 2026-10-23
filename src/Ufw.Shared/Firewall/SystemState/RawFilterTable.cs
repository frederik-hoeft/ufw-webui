namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// The filter table from one address family's <c>iptables-save</c> output.
/// </summary>
public sealed class RawFilterTable
{
    public required FirewallAddressFamily AddressFamily { get; init; }

    public required IReadOnlyList<RawFilterChain> Chains { get; init; }
}
