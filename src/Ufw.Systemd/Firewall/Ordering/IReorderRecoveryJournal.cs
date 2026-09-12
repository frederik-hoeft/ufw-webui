namespace Ufw.Systemd.Firewall.Ordering;

internal interface IReorderRecoveryJournal
{
    Task<ReorderRecoveryJournalEntry?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(ReorderRecoveryJournalEntry entry, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
