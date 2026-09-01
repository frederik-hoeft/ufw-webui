namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// ITP bootstrap and v1 frame parameters.
/// </summary>
public static class ItpConstants
{
    public const byte VERSION = 1;

    public const int MAGIC_SIZE = 3;
    public const int PREAMBLE_SIZE = MAGIC_SIZE + 1;

    public const int VERSION_1_HEADER_REMAINDER_SIZE = 6;
    public const int VERSION_1_HEADER_SIZE = PREAMBLE_SIZE + VERSION_1_HEADER_REMAINDER_SIZE;

    public const int DEFAULT_MAX_PAYLOAD_LENGTH = 16 * 1024 * 1024;
    public const int MAX_TRANSPORT_ERROR_MESSAGE_UTF_8_LENGTH = 1024;

    public static ReadOnlySpan<byte> Magic => "ITP"u8;
}
