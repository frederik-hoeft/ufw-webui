namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// Fixed v1 frame parameters. Header layout is stable across future versions so a
/// v1 peer can skip an unknown-version frame and reply with <see cref="ItpErrorCode.VersionMismatch"/>.
/// </summary>
public static class ItpConstants
{
    public const byte Version = 1;

    public const int MagicSize = 3;
    public const int HeaderSize = 10;
    public const int TrailerSize = 4;
    public const int MinimumFrameSize = HeaderSize + TrailerSize;

    public const int DefaultMaxPayloadLength = 16 * 1024 * 1024;

    public static ReadOnlySpan<byte> Magic => "ITP"u8;
}
