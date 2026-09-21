using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Model.V1.Rules;

public sealed class RuleMetadataItem
{
    public RuleMetadataItem() { }

    public RuleMetadataItem(Guid id, string ruleId, string? notes, IReadOnlyList<RuleTagItem> tags) =>
        (Id, RuleId, Notes, Tags) = (id, ruleId, notes, tags);

    public Guid Id { get; init; }

    public string RuleId { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public IReadOnlyList<RuleTagItem> Tags { get; init; } = [];
}
