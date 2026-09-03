using System.Diagnostics.CodeAnalysis;

namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// Structured ITP-level failures. These are never an application request or response.
/// </summary>
[SuppressMessage("Design", "CA1028:Enum Storage should be Int32", Justification = "ITP error codes are defined as 16-bit unsigned integers.")]
public enum ItpErrorCode : ushort
{
    None = 0,
    InvalidMagic = 0x0001,
    VersionMismatch = 0x0002,
    UnsupportedPacketType = 0x0003,
    UnsupportedPayloadFormat = 0x0004,
    IncompleteFrame = 0x0005,
    PayloadTooLarge = 0x0006,
    InvalidFrame = 0x0007,
    EmptyApplicationPayload = 0x0008,
}
