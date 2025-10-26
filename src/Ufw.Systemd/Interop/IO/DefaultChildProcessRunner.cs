using System.Diagnostics;
using System.Text;
using Ufw.Systemd.Configuration;

namespace Ufw.Systemd.Interop.IO;

internal sealed class DefaultChildProcessRunner(IConfiguration configuration) : IChildProcessRunner
{
    public async Task<int> RunAsync(string command, ReadOnlyMemory<string> arguments, Out<string> output, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command, nameof(command));

        string args = string.Join(' ', arguments.Span!);
        if (configuration.Settings.DebugMode)
        {
            Console.WriteLine($"execute: '{command} {args}'");
        }
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        StringBuilder outputBuilder = new();
        DataReceivedHandler stdoutHandler = new(outputBuilder);
        DataReceivedHandler stderrHandler = new(outputBuilder);
        process.OutputDataReceived += stdoutHandler.OnDataReceived;
        process.ErrorDataReceived += stderrHandler.OnDataReceived;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);
        process.OutputDataReceived -= stdoutHandler.OnDataReceived;
        process.ErrorDataReceived -= stderrHandler.OnDataReceived;
        output.SetValue(outputBuilder.ToString());
        if (configuration.Settings.DebugMode)
        {
            Console.WriteLine(output.Value);
        }
        return process.ExitCode;
    }

    private sealed class DataReceivedHandler(StringBuilder builder)
    {
        public void OnDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                builder.AppendLine(e.Data);
            }
        }
    }
}