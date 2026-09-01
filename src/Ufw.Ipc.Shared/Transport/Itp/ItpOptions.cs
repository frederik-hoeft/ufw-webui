namespace Ufw.Ipc.Shared.Transport.Itp;

public sealed class ItpOptions
{
    public static ItpOptions Default { get; } = new();

    /// <summary>
    /// Maximum accepted <c>PayloadLength</c>. Larger declared lengths are
    /// <see cref="ItpErrorCode.PayloadTooLarge"/> and are not read from the stream.
    /// </summary>
    public int MaxPayloadLength { get; init; } = ItpConstants.DEFAULT_MAX_PAYLOAD_LENGTH;
}
