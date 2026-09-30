namespace Ufw.Shared.Domain;

/// <summary>
/// One fully specified packet inside a family's address space. Ports are modeled ports, not zero.
/// Ingress and egress are present only for the chain that is about to be asked.
/// </summary>
public readonly record struct PacketPoint<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Creates a packet. Interface arguments that do not apply to the chain must stay null.</summary>
    public PacketPoint(
        TAddress source,
        ushort sourcePort,
        TAddress destination,
        ushort destinationPort,
        ProtocolSymbol protocol,
        NetworkInterfaceName? ingress = null,
        NetworkInterfaceName? egress = null)
    {
        if (sourcePort < PacketPorts.MINIMUM || sourcePort > PacketPorts.MAXIMUM)
        {
            throw new ArgumentOutOfRangeException(nameof(sourcePort), sourcePort, "Ports must be between 1 and 65535.");
        }

        if (destinationPort < PacketPorts.MINIMUM || destinationPort > PacketPorts.MAXIMUM)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationPort), destinationPort, "Ports must be between 1 and 65535.");
        }

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

    /// <summary>Gets the source port.</summary>
    public ushort SourcePort { get; }

    /// <summary>Gets the destination address.</summary>
    public TAddress Destination { get; }

    /// <summary>Gets the destination port.</summary>
    public ushort DestinationPort { get; }

    /// <summary>Gets the protocol.</summary>
    public ProtocolSymbol Protocol { get; }

    /// <summary>Gets the ingress interface, when the chain has one.</summary>
    public NetworkInterfaceName? Ingress { get; }

    /// <summary>Gets the egress interface, when the chain has one.</summary>
    public NetworkInterfaceName? Egress { get; }
}
