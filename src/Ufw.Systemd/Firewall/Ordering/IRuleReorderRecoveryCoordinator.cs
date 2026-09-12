using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal interface IRuleReorderRecoveryCoordinator
{
    Task<RuleRecoveryResult> EnsurePresentAsync(
        ReorderRecoveryJournalEntry entry,
        RuleListResponse? observedSnapshot,
        CancellationToken cancellationToken);
}
