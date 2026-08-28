namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// Structured ITP-level failures. These are never an application request or response.
/// </summary>
public enum ItpErrorCode : ushort
{
    None = 0,
    InvalidMagic = 0x0001,
    VersionMismatch = 0x0002,
    UnsupportedPacketType = 0x0003,
    UnsupportedFlags = 0x0004,
    IncompleteFrame = 0x0005,
    InvalidChecksum = 0x0006,
    PayloadTooLarge = 0x0007,
    InvalidFrame = 0x0008,
    EmptyApplicationPayload = 0x0009,
}
