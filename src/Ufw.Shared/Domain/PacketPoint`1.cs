namespace Ufw.Shared.Domain;

/// <summary>
/// One fully specified packet inside a family's address space. Ports are absent for protocols without port semantics.
/// Ingress and egress are present only for the chain that is about to be asked.
/// </summary>
public readonly record struct PacketPoint<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Creates a packet. Interface arguments that do not apply to the chain must stay null.</summary>
    public PacketPoint(
        TAddress source,
        ushort? sourcePort,
        TAddress destination,
        ushort? destinationPort,
        ProtocolSymbol protocol,
        NetworkInterfaceName? ingress = null,
        NetworkInterfaceName? egress = null)
    {
        ValidatePort(sourcePort, nameof(sourcePort));
        ValidatePort(destinationPort, nameof(destinationPort));
        if (string.IsNullOrEmpty(protocol.Name))
        {
            throw new ArgumentException("A protocol is required.", nameof(protocol));
        }

        if (ingress is { } ingressName && string.IsNullOrEmpty(ingressName.Name))
        {
            throw new ArgumentException("Ingress interface is invalid.", nameof(ingress));
        }

        if (egress is { } egressName && string.IsNullOrEmpty(egressName.Name))
        {
            throw new ArgumentException("Egress interface is invalid.", nameof(egress));
        }

        Source = source;
        SourcePort = sourcePort;
        Destination = destination;
        DestinationPort = destinationPort;
        Protocol = protocol;
        Ingress = ingress;
        Egress = egress;
    }

    /// <summary>Gets the source address.</summary>
    public TAddress Source { get; }

    /// <summary>Gets the source port, or <see langword="null"/> when the protocol has no port semantics.</summary>
    public ushort? SourcePort { get; }

    /// <summary>Gets the destination address.</summary>
    public TAddress Destination { get; }

    /// <summary>Gets the destination port, or <see langword="null"/> when the protocol has no port semantics.</summary>
    public ushort? DestinationPort { get; }

    /// <summary>Gets the protocol.</summary>
    public ProtocolSymbol Protocol { get; }

    /// <summary>Gets the ingress interface, when the chain has one.</summary>
    public NetworkInterfaceName? Ingress { get; }

    /// <summary>Gets the egress interface, when the chain has one.</summary>
    public NetworkInterfaceName? Egress { get; }

    private static void ValidatePort(ushort? port, string name)
    {
        if (port is < PacketPorts.MINIMUM or > PacketPorts.MAXIMUM)
        {
            throw new ArgumentOutOfRangeException(name, port, "Ports must be between 1 and 65535 when present.");
        }
    }
}
