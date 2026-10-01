namespace Ufw.Shared.Domain;

/// <summary>
/// One interface in a closed interface universe. Comparison is ordinal and case-sensitive, matching host interface names.
/// </summary>
public readonly record struct NetworkInterfaceName : IComparable<NetworkInterfaceName>
{
    /// <summary>Creates a trimmed interface name.</summary>
    public NetworkInterfaceName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>Gets the interface name.</summary>
    public string Name { get; }

    /// <inheritdoc />
    public int CompareTo(NetworkInterfaceName other) => string.CompareOrdinal(Name ?? string.Empty, other.Name ?? string.Empty);

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(NetworkInterfaceName left, NetworkInterfaceName right) => left.CompareTo(right) < 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(NetworkInterfaceName left, NetworkInterfaceName right) => left.CompareTo(right) > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before or with <paramref name="right"/>.</summary>
    public static bool operator <=(NetworkInterfaceName left, NetworkInterfaceName right) => left.CompareTo(right) <= 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after or with <paramref name="right"/>.</summary>
    public static bool operator >=(NetworkInterfaceName left, NetworkInterfaceName right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Name ?? string.Empty;
}
