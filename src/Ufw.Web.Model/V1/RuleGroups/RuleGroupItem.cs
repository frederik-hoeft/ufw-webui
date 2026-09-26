namespace Ufw.Web.Model.V1.RuleGroups;

public sealed class RuleGroupItem
{
    public RuleGroupItem() { }

    public RuleGroupItem(Guid id, string name, string? comment, IReadOnlyList<string> ruleIds) =>
        (Id, Name, Comment, RuleIds) = (id, name, comment, ruleIds);

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public IReadOnlyList<string> RuleIds { get; init; } = [];
}
