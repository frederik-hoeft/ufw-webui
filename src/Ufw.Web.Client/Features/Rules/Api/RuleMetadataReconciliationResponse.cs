namespace Ufw.Web.Client.Features.Rules.Api;

public sealed class RuleMetadataReconciliationResponse
{
    public IReadOnlyList<RuleMetadataItem> Orphans { get; init; } = [];

    public int RemovedCount { get; init; }
}
