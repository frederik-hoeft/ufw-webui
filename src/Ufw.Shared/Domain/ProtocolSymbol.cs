using System.Diagnostics.CodeAnalysis;

namespace Ufw.Shared.Domain;

/// <summary>
/// One protocol in a closed protocol universe. The value is an opaque lowercase symbol, not a fixed enum,
/// so a world can name protocols the evaluator does not hard-code.
/// </summary>
public readonly record struct ProtocolSymbol : IComparable<ProtocolSymbol>
{
    /// <summary>Creates a symbol. The name is trimmed and compared case-insensitively.</summary>
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "UFW protocol names are lowercase, and that text is the canonical symbol.")]
    public ProtocolSymbol(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        // UFW prints protocol names in lowercase. That is the canonical symbol, not an arbitrary case fold.
        Name = name.Trim().ToLowerInvariant();
    }

    /// <summary>Gets the canonical protocol name.</summary>
    public string Name { get; }

    /// <summary>Gets the TCP symbol used by the UFW projection.</summary>
    public static ProtocolSymbol Tcp => new("tcp");

    /// <summary>Gets the UDP symbol used by the UFW projection.</summary>
    public static ProtocolSymbol Udp => new("udp");

    /// <inheritdoc />
    public int CompareTo(ProtocolSymbol other) => string.CompareOrdinal(Name ?? string.Empty, other.Name ?? string.Empty);

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(ProtocolSymbol left, ProtocolSymbol right) => left.CompareTo(right) < 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(ProtocolSymbol left, ProtocolSymbol right) => left.CompareTo(right) > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before or with <paramref name="right"/>.</summary>
    public static bool operator <=(ProtocolSymbol left, ProtocolSymbol right) => left.CompareTo(right) <= 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after or with <paramref name="right"/>.</summary>
    public static bool operator >=(ProtocolSymbol left, ProtocolSymbol right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Name ?? string.Empty;
}
