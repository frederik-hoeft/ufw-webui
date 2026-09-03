using System.Collections.Immutable;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Interop.IO;

internal sealed class UfwRunner(IConfiguration configuration, IChildProcessRunner processRunner) : IUfwRunner
{
    private static readonly ImmutableDictionary<string, string> s_environment = ImmutableDictionary<string, string>.Empty
        .Add("LC_ALL", "C")
        .Add("LANG", "C");

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

        ChildProcessRequest request = new(configuration.Settings.UfwPath, args, s_environment);
        ChildProcessResult result = await processRunner.RunAsync(request, cancellationToken);
        command.SetOutput(result.StandardOutput);
        return new UfwProcessResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            args,
            result.CancellationRequested);
    }
}
