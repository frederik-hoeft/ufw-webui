using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Model.V1.RuleGroups;

public sealed class CreateRuleGroupRequest
{
    [Required]
    [StringLength(RuleGroupLimits.MAX_NAME_LENGTH, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(RuleGroupLimits.MAX_COMMENT_LENGTH)]
    public string? Comment { get; init; }
}
