namespace Ufw.Web.Client.Features.Rules.Metadata;

public interface IRuleMetadataReconciliationService
{
    Task<RuleMetadataReconciliationSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataReconciliationSnapshot> CleanupAsync(IReadOnlyCollection<Guid> metadataIds, CancellationToken cancellationToken = default);
}
