namespace Ufw.Web.Api.V1.Models.Rules;

public sealed class CleanupRuleMetadataRequest
{
    public IReadOnlyList<Guid> MetadataIds { get; init; } = [];
}
