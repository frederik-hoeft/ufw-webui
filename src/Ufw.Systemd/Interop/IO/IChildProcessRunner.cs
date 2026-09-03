namespace Ufw.Systemd.Interop.IO;

internal interface IChildProcessRunner
{
    Task<ChildProcessResult> RunAsync(ChildProcessRequest request, CancellationToken cancellationToken);
}
