namespace Ufw.Ipc.Shared.Protocol;

/// <summary>
/// The ITP frame was valid, but the application document is not a v1 request or response.
/// </summary>
public sealed class ApplicationProtocolException : Exception
{
    public ApplicationProtocolException(ApplicationProtocolError error, string message)
        : base(message)
    {
        Error = error;
    }

    public ApplicationProtocolException(ApplicationProtocolError error, string message, Exception innerException)
        : base(message, innerException)
    {
        Error = error;
    }

    public ApplicationProtocolError Error { get; }
}
