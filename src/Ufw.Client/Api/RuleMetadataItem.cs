namespace Ufw.Client.Api;

public sealed class RuleMetadataItem
{
    public Guid Id { get; init; }

    public string RuleId { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
