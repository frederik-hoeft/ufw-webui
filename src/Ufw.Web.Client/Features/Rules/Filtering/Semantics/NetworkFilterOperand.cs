using Ufw.Shared.Domain.Algebra;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules.Filtering.Semantics;

/// <summary>
/// A filter-facing network operand. Address membership and relationships are evaluated by the shared numeric interval domain.
/// </summary>
internal sealed class NetworkFilterOperand : IEquatable<NetworkFilterOperand>
{
    internal NetworkFilterOperand(FirewallAddressFamily addressFamily, IntervalSet<uint> ipv4, IntervalSet<UInt128> ipv6, string canonicalValue)
    {
        AddressFamily = addressFamily;
        IPv4 = ipv4;
        IPv6 = ipv6;
        CanonicalValue = canonicalValue;
    }

    public FirewallAddressFamily AddressFamily { get; }

    public string CanonicalValue { get; }

    private IntervalSet<uint> IPv4 { get; }

    private IntervalSet<UInt128> IPv6 { get; }

    public bool Equals(NetworkFilterOperand? other) => other is not null && SetEquals(other);

    public override bool Equals(object? obj) => obj is NetworkFilterOperand other && Equals(other);

    public override int GetHashCode() => AddressFamily switch
    {
        FirewallAddressFamily.IPv4 => HashCode.Combine(AddressFamily, IPv4),
        FirewallAddressFamily.IPv6 => HashCode.Combine(AddressFamily, IPv6),
        _ => HashCode.Combine(AddressFamily),
    };

    public bool SetEquals(NetworkFilterOperand other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return AddressFamily == other.AddressFamily && (AddressFamily switch
        {
            FirewallAddressFamily.IPv4 => IPv4.Equals(other.IPv4),
            FirewallAddressFamily.IPv6 => IPv6.Equals(other.IPv6),
            _ => false,
        });
    }

    public bool Contains(NetworkFilterOperand other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return AddressFamily == other.AddressFamily && (AddressFamily switch
        {
            FirewallAddressFamily.IPv4 => IPv4.IsSupersetOf(other.IPv4),
            FirewallAddressFamily.IPv6 => IPv6.IsSupersetOf(other.IPv6),
            _ => false,
        });
    }

    public bool Overlaps(NetworkFilterOperand other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return AddressFamily == other.AddressFamily && (AddressFamily switch
        {
            FirewallAddressFamily.IPv4 => IPv4.Overlaps(other.IPv4),
            FirewallAddressFamily.IPv6 => IPv6.Overlaps(other.IPv6),
            _ => false,
        });
    }
}
