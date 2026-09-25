namespace Ufw.Web.Model.V1.RuleMetadata;

public sealed class CleanupRuleMetadataRequest
{
    public IReadOnlyList<Guid> MetadataIds { get; init; } = [];
}
