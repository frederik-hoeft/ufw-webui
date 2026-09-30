namespace Ufw.Shared.Domain;

/// <summary>
/// One protocol in the closed world together with the packet dimensions that apply to it.
/// </summary>
public readonly record struct ProtocolDefinition : IComparable<ProtocolDefinition>
{
    /// <summary>Creates a protocol definition.</summary>
    public ProtocolDefinition(ProtocolSymbol symbol, bool usesPorts)
    {
        if (string.IsNullOrEmpty(symbol.Name))
        {
            throw new ArgumentException("A protocol symbol is required.", nameof(symbol));
        }

        Symbol = symbol;
        UsesPorts = usesPorts;
    }

    /// <summary>Gets the protocol symbol used by rules and queries.</summary>
    public ProtocolSymbol Symbol { get; }

    /// <summary>Gets a value indicating whether source and destination ports apply to this protocol.</summary>
    public bool UsesPorts { get; }

    /// <summary>Gets the TCP definition.</summary>
    public static ProtocolDefinition Tcp => new(ProtocolSymbol.Tcp, usesPorts: true);

    /// <summary>Gets the UDP definition.</summary>
    public static ProtocolDefinition Udp => new(ProtocolSymbol.Udp, usesPorts: true);

    /// <inheritdoc />
    public int CompareTo(ProtocolDefinition other)
    {
        int compared = Symbol.CompareTo(other.Symbol);
        return compared != 0 ? compared : UsesPorts.CompareTo(other.UsesPorts);
    }

    /// <summary>Returns whether <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(ProtocolDefinition left, ProtocolDefinition right) => left.CompareTo(right) < 0;

    /// <summary>Returns whether <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(ProtocolDefinition left, ProtocolDefinition right) => left.CompareTo(right) > 0;

    /// <summary>Returns whether <paramref name="left"/> sorts before or with <paramref name="right"/>.</summary>
    public static bool operator <=(ProtocolDefinition left, ProtocolDefinition right) => left.CompareTo(right) <= 0;

    /// <summary>Returns whether <paramref name="left"/> sorts after or with <paramref name="right"/>.</summary>
    public static bool operator >=(ProtocolDefinition left, ProtocolDefinition right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => UsesPorts ? $"{Symbol} (ports)" : Symbol.ToString();
}
