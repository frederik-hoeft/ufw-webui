namespace Ufw.Web.Client.Features.Rules.Metadata;

public sealed record RuleGroup(Guid Id, string Name, string? Comment, IReadOnlyList<string> RuleIds, IReadOnlyList<Guid> TemplateIds)
{
    public RuleGroup(Guid id, string name, string? comment, IReadOnlyList<string> ruleIds) : this(id, name, comment, ruleIds, []) { }
}
