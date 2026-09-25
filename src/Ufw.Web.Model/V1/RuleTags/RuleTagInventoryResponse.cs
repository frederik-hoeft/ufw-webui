namespace Ufw.Web.Model.V1.RuleTags;

public sealed class RuleTagInventoryResponse
{
    public RuleTagInventoryResponse() { }

    public RuleTagInventoryResponse(IReadOnlyList<RuleTagItem> tags) => Tags = tags;

    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
