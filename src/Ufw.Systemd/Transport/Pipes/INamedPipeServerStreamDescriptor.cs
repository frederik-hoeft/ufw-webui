using System.IO.Pipes;

namespace Ufw.Systemd.Transport.Pipes;

/// <summary>
/// Accepts named-pipe connections and transfers ownership of fully initialized, connected server streams to callers.
/// </summary>
internal interface INamedPipeServerStreamDescriptor
{
    /// <summary>
    /// Creates and configures a server stream, waits for a client connection, and transfers ownership only after the accept succeeds.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pending accept before ownership is transferred.</param>
    /// <returns>A connected server stream owned by the caller.</returns>
    Task<NamedPipeServerStream> ServeAsync(CancellationToken cancellationToken);
}
