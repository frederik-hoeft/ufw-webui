namespace Ufw.Ipc.Shared.Transport.Itp;

public readonly struct ItpFrame
{
    public ItpFrame(ItpPacketType packetType, ItpPayloadFormat payloadFormat, ReadOnlyMemory<byte> payload)
    {
        PacketType = packetType;
        PayloadFormat = payloadFormat;
        Payload = payload;
    }

    public ItpPacketType PacketType { get; }

    public ItpPayloadFormat PayloadFormat { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public bool IsApplicationData => PacketType == ItpPacketType.ApplicationData;

    public bool IsTransportError => PacketType == ItpPacketType.TransportError;
}
