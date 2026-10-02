namespace Ufw.Systemd.Interop.IO;

/// <summary>
/// Executes a child process while retaining ownership of its lifetime, output capture, cancellation, and reaping semantics.
/// </summary>
internal interface IChildProcessRunner
{
    /// <summary>
    /// Executes <paramref name="request"/> and returns the completed process result. Cancellation after process start is reported in the result only after the child has been reaped.
    /// </summary>
    Task<ChildProcessResult> RunAsync(ChildProcessRequest request, CancellationToken cancellationToken);
}
