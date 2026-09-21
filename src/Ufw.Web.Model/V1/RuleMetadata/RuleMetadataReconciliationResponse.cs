using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Model.V1.RuleMetadata;

public sealed class RuleMetadataReconciliationResponse
{
    public RuleMetadataReconciliationResponse() { }

    public RuleMetadataReconciliationResponse(IReadOnlyList<RuleMetadataItem> orphans, int removedCount = 0) =>
        (Orphans, RemovedCount) = (orphans, removedCount);

    public IReadOnlyList<RuleMetadataItem> Orphans { get; init; } = [];

    public int RemovedCount { get; init; }
}
