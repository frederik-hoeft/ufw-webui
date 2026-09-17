namespace Ufw.Client.Rules.Metadata;

public sealed record RuleMetadataReconciliationSnapshot(
    IReadOnlyList<OrphanedRuleMetadata> Orphans,
    int RemovedCount);
