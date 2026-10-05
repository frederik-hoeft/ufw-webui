using System.ComponentModel.DataAnnotations;

namespace Ufw.Web.Model.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NoEmptyGuidsAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is IEnumerable<Guid> ids && !ids.Contains(Guid.Empty);
}
