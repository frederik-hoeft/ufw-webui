using System.Diagnostics.CodeAnalysis;

namespace Ufw.Ipc.Shared.Transport.Itp;

[SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "ITP packet type is a single byte.")]
public enum ItpPacketType : byte
{
    None = 0,
    ApplicationData = 0x01,
    TransportError = 0x02,
}
