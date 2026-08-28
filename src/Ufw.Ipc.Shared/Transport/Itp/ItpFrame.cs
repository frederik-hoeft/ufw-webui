namespace Ufw.Ipc.Shared.Transport.Itp;

public readonly struct ItpFrame
{
    public ItpFrame(ItpPacketType packetType, ReadOnlyMemory<byte> payload)
    {
        PacketType = packetType;
        Payload = payload;
    }

    public ItpPacketType PacketType { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public bool IsApplicationData => PacketType == ItpPacketType.ApplicationData;

    public bool IsTransportError => PacketType == ItpPacketType.TransportError;
}
