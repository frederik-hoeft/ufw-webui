using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Model.Validation;

namespace Ufw.Web.Model.V1.Rules;

public sealed class UpdateRuleMetadataRequest
{
    [StringLength(RuleMetadataLimits.MAX_NOTES_LENGTH)]
    public string? Notes { get; init; }

    [Required]
    [MaxLength(RuleMetadataLimits.MAX_TAG_COUNT)]
    [NoEmptyGuids]
    public IReadOnlyList<Guid> TagIds { get; init; } = [];

    [NotEmptyGuid]
    public Guid? GroupId { get; init; }
}
