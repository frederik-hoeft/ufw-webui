using Ufw.Web.Model.V1.RuleGroups;
using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Model.V1.Rules;

public sealed class RuleMetadataItem
{
    public RuleMetadataItem() { }

    public RuleMetadataItem(Guid id, string ruleId, string? notes, IReadOnlyList<RuleTagItem> tags, RuleGroupSummary? group = null) =>
        (Id, RuleId, Notes, Tags, Group) = (id, ruleId, notes, tags, group);

    public Guid Id { get; init; }

    public string RuleId { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];

    public RuleGroupSummary? Group { get; init; }
}
