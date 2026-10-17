namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Read-only structural snapshot of the active IPv4 and IPv6 filter tables.
/// Independent of the mutable UFW rule list, with optional links to known user rules.
/// </summary>
public sealed class FilterSystemState
{
    public required AddressFamilyFilterState IPv4 { get; init; }

    public required AddressFamilyFilterState IPv6 { get; init; }
}
