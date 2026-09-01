namespace Ufw.Ipc.Shared.Transport.Itp;

public enum ItpPacketType : byte
{
    None = 0,
    ApplicationData = 0x01,
    TransportError = 0x02,
}
