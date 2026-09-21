using Ufw.Web.Client.Api.Rules.Model;
namespace Ufw.Web.Client.Api.RuleMetadata.Model;

public sealed class RuleMetadataReconciliationResponse
{
    public IReadOnlyList<RuleMetadataItem> Orphans { get; init; } = [];

    public int RemovedCount { get; init; }
}
