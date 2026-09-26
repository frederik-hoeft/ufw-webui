namespace Ufw.Web.Model.V1.RuleGroups;

public sealed class UpdateRuleGroupRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Comment { get; init; }
}
