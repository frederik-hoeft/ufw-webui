namespace Ufw.Web.Api.V1.Models.Rules;

public sealed record RuleMetadataReconciliationResponse(
    IReadOnlyList<RuleMetadataItem> Orphans,
    int RemovedCount = 0);
