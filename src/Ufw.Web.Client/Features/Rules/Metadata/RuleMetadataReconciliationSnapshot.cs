namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record RuleMetadataReconciliationSnapshot(IReadOnlyList<OrphanedRuleMetadata> Orphans, int RemovedCount);
