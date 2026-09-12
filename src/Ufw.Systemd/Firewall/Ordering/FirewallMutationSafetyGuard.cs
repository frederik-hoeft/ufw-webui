namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallMutationSafetyGuard(
    IReorderRecoveryJournal journal,
    IRuleReorderRecoveryCoordinator recoveryCoordinator) : IFirewallMutationSafetyGuard
{
    public async Task EnsureSafeAsync(CancellationToken cancellationToken)
    {
        ReorderRecoveryJournalEntry? entry = await journal.ReadAsync(cancellationToken);
        if (entry is null)
        {
            return;
        }

        RuleRecoveryResult result = await recoveryCoordinator.EnsurePresentAsync(entry, null, CancellationToken.None);
        if (!result.PresenceConfirmed)
        {
            throw new InvalidOperationException(
                "An interrupted firewall reorder move could not be recovered safely. Firewall mutations remain blocked until the recovery condition is resolved.");
        }
    }
}
