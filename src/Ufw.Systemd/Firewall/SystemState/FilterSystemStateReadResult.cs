using Ufw.Shared.Firewall.SystemState;

namespace Ufw.Systemd.Firewall.SystemState;

internal abstract record FilterSystemStateReadResult
{
    private FilterSystemStateReadResult()
    {
    }

    internal sealed record Success(FilterSystemState State) : FilterSystemStateReadResult;

    internal sealed record Disabled : FilterSystemStateReadResult;

    internal sealed record Failure(string Diagnostic) : FilterSystemStateReadResult;
}
