namespace Ufw.Systemd.Interop.IO;

internal interface IChildProcessRunner
{
    Task<int> RunAsync(string command, ReadOnlyMemory<string> arguments, Out<string> output, CancellationToken cancellationToken);
}