using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.Rules;
using Ufw.Web.Model.Validation;

namespace Ufw.Web.Model.V1.RuleTemplates;

public abstract class RuleTemplateRequest
{
    [Required]
    [StringLength(RuleTemplateLimits.MAX_NAME_LENGTH, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(RuleTemplateLimits.MAX_DESCRIPTION_LENGTH)]
    public string? Description { get; init; }

    [Required]
    public FirewallRuleSpecification Rule { get; init; } = null!;

    [StringLength(RuleMetadataLimits.MAX_NOTES_LENGTH)]
    public string? Notes { get; init; }

    [JsonRequired]
    [Required]
    [MaxLength(RuleMetadataLimits.MAX_TAG_COUNT)]
    [NoEmptyGuids]
    public IReadOnlyList<Guid> TagIds { get; init; } = [];

    [NotEmptyGuid]
    public Guid? GroupId { get; init; }
}
