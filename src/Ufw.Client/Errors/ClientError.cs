namespace Ufw.Client.Errors;

public enum ClientErrorKind
{
    Unavailable,
    Unauthorized,
    Forbidden,
    RequestRejected,
    Conflict,
    Protocol,
    Browser,
    Canceled,
    Unexpected,
}

public sealed record ClientError(ClientErrorKind Kind, string Message, bool Retryable);
