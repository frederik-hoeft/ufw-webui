namespace Ufw.Client.Api;

public sealed class RuleMetadataItem
{
    public string RuleId { get; init; } = string.Empty;

    public string? Group { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];
}
