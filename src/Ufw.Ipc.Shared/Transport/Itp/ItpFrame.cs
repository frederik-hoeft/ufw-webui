namespace Ufw.Ipc.Shared.Transport.Itp;

public readonly struct ItpFrame(ItpPacketType packetType, ItpPayloadFormat payloadFormat, ReadOnlyMemory<byte> payload)
{
    public ItpPacketType PacketType { get; } = packetType;

    public ItpPayloadFormat PayloadFormat { get; } = payloadFormat;

    public ReadOnlyMemory<byte> Payload { get; } = payload;

    public bool IsApplicationData => PacketType == ItpPacketType.ApplicationData;

    public bool IsTransportError => PacketType == ItpPacketType.TransportError;
}
