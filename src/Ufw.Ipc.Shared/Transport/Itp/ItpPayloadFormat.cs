using System.Diagnostics.CodeAnalysis;

namespace Ufw.Ipc.Shared.Transport.Itp;

[SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "ITP frame format requires a single byte for the payload format.")]
public enum ItpPayloadFormat : byte
{
    None = 0,
    IpcJson = 0x01,
}
