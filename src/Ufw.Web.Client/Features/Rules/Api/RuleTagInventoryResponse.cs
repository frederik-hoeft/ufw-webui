namespace Ufw.Web.Client.Features.Rules.Api;

public sealed class RuleTagInventoryResponse
{
    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
