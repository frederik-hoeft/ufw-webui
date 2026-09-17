namespace Ufw.Client.Api;

public sealed class UpdateRuleMetadataRequest
{
    public string? Notes { get; init; }

    public IReadOnlyList<Guid> TagIds { get; init; } = [];
}
