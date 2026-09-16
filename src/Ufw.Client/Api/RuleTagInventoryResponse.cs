namespace Ufw.Client.Api;

public sealed class RuleTagInventoryResponse
{
    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
