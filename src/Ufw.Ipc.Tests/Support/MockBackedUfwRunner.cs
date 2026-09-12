using System.Collections.Immutable;
using System.Globalization;
using Ufw.Mock;
using Ufw.Systemd.Interop.Commands;
using Ufw.Systemd.Interop.IO;

namespace Ufw.Ipc.Tests.Support;

internal sealed class MockBackedUfwRunner(string statePath) : IUfwRunner
{
    private static readonly SemaphoreSlim s_consoleGate = new(1, 1);

    public Func<ImmutableArray<string>, CancellationToken, ValueTask>? AfterCommandAsync { get; set; }

    public async Task<UfwProcessResult> ExecuteAsync(IUfwCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        ImmutableArray<string> arguments = command.BuildArguments();
        MockCommandResult result = await InvokeAsync(statePath, arguments, cancellationToken);
        command.SetOutput(result.StandardOutput);
        if (AfterCommandAsync is not null)
        {
            await AfterCommandAsync(arguments, cancellationToken);
        }

        return new UfwProcessResult(
            result.ExitCode,
            result.StandardOutput,
            result.StandardError,
            arguments,
            CancellationRequested: false);
    }

    public static async Task<MockCommandResult> InvokeAsync(
        string statePath,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(arguments);
        cancellationToken.ThrowIfCancellationRequested();

        await s_consoleGate.WaitAsync(cancellationToken);
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        string? originalStatePath = Environment.GetEnvironmentVariable("UFW_MOCK_STATE_PATH");
        using StringWriter stdout = new(CultureInfo.InvariantCulture);
        using StringWriter stderr = new(CultureInfo.InvariantCulture);
        try
        {
            Environment.SetEnvironmentVariable("UFW_MOCK_STATE_PATH", statePath);
            Console.SetOut(stdout);
            Console.SetError(stderr);
            int exitCode = await UfwMockApplication.RunAsync(arguments.ToArray());
            return new MockCommandResult(exitCode, Normalize(stdout.ToString()), Normalize(stderr.ToString()));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable("UFW_MOCK_STATE_PATH", originalStatePath);
            s_consoleGate.Release();
        }
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
}

internal sealed record MockCommandResult(int ExitCode, string StandardOutput, string StandardError);
