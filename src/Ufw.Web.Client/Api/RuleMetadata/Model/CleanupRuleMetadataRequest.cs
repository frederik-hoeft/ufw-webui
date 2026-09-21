namespace Ufw.Web.Client.Api.RuleMetadata.Model;

public sealed class CleanupRuleMetadataRequest
{
    public IReadOnlyList<Guid> MetadataIds { get; init; } = [];
}
