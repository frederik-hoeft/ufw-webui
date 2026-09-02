using System.Collections.Immutable;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Interop.IO;

internal sealed class UfwRunner(IConfiguration configuration, IChildProcessRunner processRunner) : IUfwRunner
{
    public async Task<UfwProcessResult> ExecuteAsync(IUfwCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ImmutableArray<string> args = command.BuildArguments();
        foreach (string argument in args)
        {
            if (argument.IndexOfAny(['\0', '\n', '\r']) >= 0)
            {
                throw new InvalidOperationException("Refusing to execute a UFW argument that contains a control character.");
            }
        }

        string ufw = configuration.Settings.UfwPath;
        Out<string> output = new();
        int exitCode = await processRunner.RunAsync(ufw, args, output, cancellationToken);
        string outputValue = output.TryGetValue(out string? value) ? value : string.Empty;
        command.SetOutput(outputValue);
        return new UfwProcessResult(exitCode, outputValue, args);
    }
}
