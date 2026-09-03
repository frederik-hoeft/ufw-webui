using System.Diagnostics.CodeAnalysis;

namespace Ufw.Ipc.Shared.Protocol;

/// <summary>
/// The ITP frame was valid, but the application document is not a v1 request or response.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Designed to be thrown with a specific error code and message.")]
public sealed class ApplicationProtocolException : Exception
{
    public ApplicationProtocolException(ApplicationProtocolError error, string message) : base(message)
    {
        Error = error;
    }

    public ApplicationProtocolException(ApplicationProtocolError error, string message, Exception innerException) : base(message, innerException)
    {
        Error = error;
    }

    public ApplicationProtocolError Error { get; }
}
