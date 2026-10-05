using System.ComponentModel.DataAnnotations;
using Ufw.Web.Model.Validation;

namespace Ufw.Web.Model.V1.RuleMetadata;

public sealed class CleanupRuleMetadataRequest
{
    [Required]
    [MinLength(1)]
    [NoEmptyGuids]
    public IReadOnlyList<Guid> MetadataIds { get; init; } = [];
}
