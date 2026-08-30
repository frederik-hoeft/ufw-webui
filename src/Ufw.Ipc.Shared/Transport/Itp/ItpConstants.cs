namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// ITP bootstrap and v1 frame parameters.
/// </summary>
public static class ItpConstants
{
    public const byte Version = 1;

    public const int MagicSize = 3;
    public const int PreambleSize = MagicSize + 1;

    public const int Version1HeaderRemainderSize = 6;
    public const int Version1HeaderSize = PreambleSize + Version1HeaderRemainderSize;

    public const int DefaultMaxPayloadLength = 16 * 1024 * 1024;
    public const int MaxTransportErrorMessageUtf8Length = 1024;

    public static ReadOnlySpan<byte> Magic => "ITP"u8;
}
