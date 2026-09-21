namespace Ufw.Web.Client.Api.RuleTags.Model;

public sealed class RuleTagInventoryResponse
{
    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
