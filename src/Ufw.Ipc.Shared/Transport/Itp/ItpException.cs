namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// ITP protocol failure. The application protocol decoder is not invoked for the offending frame.
/// </summary>
public sealed class ItpException : Exception
{
    public ItpException(ItpErrorCode errorCode, string message, bool isPeerReported = false)
        : this(errorCode, message, innerException: null, isPeerReported, canReplyWithTransportError: false)
    {
    }

    public ItpException(ItpErrorCode errorCode, string message, Exception innerException, bool isPeerReported = false)
        : this(errorCode, message, innerException, isPeerReported, canReplyWithTransportError: false)
    {
    }

    private ItpException(
        ItpErrorCode errorCode,
        string message,
        Exception? innerException,
        bool isPeerReported,
        bool canReplyWithTransportError)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsPeerReported = isPeerReported;
        CanReplyWithTransportError = canReplyWithTransportError;
    }

    public ItpErrorCode ErrorCode { get; }

    /// <summary>
    /// <see langword="true"/> when this exception represents a <see cref="ItpPacketType.TransportError"/>
    /// sent by the peer, rather than a locally detected framing failure.
    /// </summary>
    public bool IsPeerReported { get; }

    /// <summary>
    /// <see langword="true"/> when the receiver has established enough v1 context to safely report this
    /// locally detected failure as a <see cref="ItpPacketType.TransportError"/>.
    /// </summary>
    public bool CanReplyWithTransportError { get; }

    public static ItpException Local(ItpErrorCode errorCode, string message) =>
        new(errorCode, message, innerException: null, isPeerReported: false, canReplyWithTransportError: false);

    internal static ItpException Local(
        ItpErrorCode errorCode,
        string message,
        bool canReplyWithTransportError) =>
        new(errorCode, message, innerException: null, isPeerReported: false, canReplyWithTransportError);

    public static ItpException PeerReported(ItpErrorCode errorCode, string message) =>
        new(errorCode, message, innerException: null, isPeerReported: true, canReplyWithTransportError: false);
}
