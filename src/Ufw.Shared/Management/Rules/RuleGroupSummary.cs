namespace Ufw.Shared.Management.Rules;

public sealed class RuleGroupSummary
{
    public RuleGroupSummary() { }

    public RuleGroupSummary(Guid id, string name, string? comment) => (Id, Name, Comment) = (id, name, comment);

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Comment { get; init; }
}
