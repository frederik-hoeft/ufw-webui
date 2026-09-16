namespace Ufw.Client.Api;

public sealed class UpdateRuleMetadataRequest
{
    public string? Group { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
