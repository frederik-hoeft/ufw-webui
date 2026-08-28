namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// Transport-layer failure. The application protocol decoder is not invoked for the offending frame.
/// </summary>
public sealed class ItpException : IOException
{
    public ItpException(ItpErrorCode errorCode, string message, bool isPeerReported = false)
        : base(message)
    {
        ErrorCode = errorCode;
        IsPeerReported = isPeerReported;
    }

    public ItpException(ItpErrorCode errorCode, string message, Exception innerException, bool isPeerReported = false)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsPeerReported = isPeerReported;
    }

    public ItpErrorCode ErrorCode { get; }

    /// <summary>
    /// <see langword="true"/> when this exception represents a <see cref="ItpPacketType.TransportError"/>
    /// sent by the peer, rather than a locally detected framing failure.
    /// </summary>
    public bool IsPeerReported { get; }

    public static ItpException Local(ItpErrorCode errorCode, string message) =>
        new(errorCode, message, isPeerReported: false);

    public static ItpException PeerReported(ItpErrorCode errorCode, string message) =>
        new(errorCode, message, isPeerReported: true);
}
