namespace Ufw.Client.Api;

public sealed class CleanupRuleMetadataRequest
{
    public IReadOnlyList<Guid> MetadataIds { get; init; } = [];
}
