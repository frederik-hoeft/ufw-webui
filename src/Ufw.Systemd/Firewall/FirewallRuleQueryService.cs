using Ufw.Shared.Ipc.Model;

namespace Ufw.Systemd.Firewall;

internal sealed class FirewallRuleQueryService(IFirewallRuleSnapshotReader snapshotReader, IUfwExecutionGate executionGate) : IFirewallRuleQueryService
{
    public ValueTask<IResponsePayload> ListAsync(CancellationToken cancellationToken) =>
        new(executionGate.RunAsync(ListUnsynchronizedAsync, cancellationToken));

    private async Task<IResponsePayload> ListUnsynchronizedAsync(CancellationToken cancellationToken)
    {
        FirewallRuleSnapshotReadResult result = await snapshotReader.ReadAsync(cancellationToken);
        return result.Error ?? FirewallRuleSet.ToListResponse(result.Snapshot!);
    }
}
