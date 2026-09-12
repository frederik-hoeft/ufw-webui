using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Firewall.Insertion;

internal sealed class FirewallOrderedInsertionService(
    IUfwExecutionGate executionGate,
    IFirewallMutationSafetyGuard mutationSafetyGuard,
    IFirewallOrderedInsertionExecutor executor) : IFirewallOrderedInsertionService
{
    public async ValueTask<RuleInsertionExecutionResult> InsertAsync(
        InsertRulePayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return await executionGate.RunAsync(async ct =>
        {
            await mutationSafetyGuard.EnsureSafeAsync(ct);
            return await executor.ExecuteAsync(payload, ct);
        }, cancellationToken);
    }
}
