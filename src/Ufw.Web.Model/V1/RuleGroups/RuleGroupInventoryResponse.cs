namespace Ufw.Web.Model.V1.RuleGroups;

public sealed class RuleGroupInventoryResponse
{
    public RuleGroupInventoryResponse() { }

    public RuleGroupInventoryResponse(IReadOnlyList<RuleGroupItem> groups) => Groups = groups;

    public IReadOnlyList<RuleGroupItem> Groups { get; init; } = [];
}
