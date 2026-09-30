namespace Ufw.Shared.Domain;

/// <summary>
/// Concrete address family of one policy world. IPv4 and IPv6 are never evaluated together.
/// </summary>
public enum IpFamily
{
    /// <summary>The IPv4 address space.</summary>
    IPv4 = 0,

    /// <summary>The IPv6 address space.</summary>
    IPv6 = 1,
}
