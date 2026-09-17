namespace Ufw.Client.Rules.Metadata;

public interface IRuleMetadataReconciliationService
{
    Task<RuleMetadataReconciliationSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataReconciliationSnapshot> CleanupAsync(
        IReadOnlyCollection<Guid> metadataIds,
        CancellationToken cancellationToken = default);
}
