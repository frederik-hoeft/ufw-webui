using Ufw.Systemd.Configuration;
using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Interop.IO;

internal sealed class UfwRunner(IConfiguration configuration, IChildProcessRunner processRunner) : IUfwRunner
{
    public async Task<bool> RunAsync(IUfwCommand command, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<string> args = command.BuildArguments();
        string ufw = configuration.Settings.UfwPath;
        Out<string> output = new();
        int exitCode = await processRunner.RunAsync(ufw, args, output, cancellationToken);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"ufw failed unexpectedly with exit code {exitCode} while running '{ufw} {string.Join(' ', args.Span!)}': {output.Value}");
        }
        if (!output.TryGetValue(out string? outputValue))
        {
            throw new InvalidOperationException("ufw output is empty");
        }
        command.SetOutput(outputValue);
        return true;
    }
}
