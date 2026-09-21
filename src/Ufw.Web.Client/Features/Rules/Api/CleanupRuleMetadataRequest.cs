namespace Ufw.Web.Client.Features.Rules.Api;

public sealed class CleanupRuleMetadataRequest
{
    public IReadOnlyList<Guid> MetadataIds { get; init; } = [];
}
