using System.Collections.Immutable;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.Output;

namespace Ufw.Systemd.Interop.Commands;

internal sealed class UfwListCommand : IUfwCommand<UfwStatusSnapshot>
{
    private static readonly ImmutableArray<string> s_arguments = UfwRuleArgumentBuilder.BuildList();
    private string? _output;

    public ImmutableArray<string> BuildArguments() => s_arguments;

    public void SetOutput(string output) => _output = output;

    public ValueTask<UfwStatusSnapshot?> GetResultAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_output is null)
        {
            return ValueTask.FromResult<UfwStatusSnapshot?>(null);
        }

        return ValueTask.FromResult<UfwStatusSnapshot?>(UfwStatusParser.Parse(_output));
    }
}
