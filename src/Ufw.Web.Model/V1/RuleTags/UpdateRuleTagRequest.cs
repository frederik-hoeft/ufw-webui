using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.Rules;

namespace Ufw.Web.Model.V1.RuleTags;

public sealed class UpdateRuleTagRequest
{
    [Required]
    [StringLength(RuleTagLimits.MAX_NAME_LENGTH, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(RuleTagLimits.COLOR_LENGTH, MinimumLength = RuleTagLimits.COLOR_LENGTH)]
    [RegularExpression(RuleTagRequestValidation.HEX_COLOR_PATTERN)]
    public string Color { get; init; } = string.Empty;
}
