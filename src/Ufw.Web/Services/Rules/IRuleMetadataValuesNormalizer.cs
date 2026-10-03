using Ufw.Web.Data.Access.Rules;
using System.Diagnostics.CodeAnalysis;

namespace Ufw.Web.Services.Rules;

internal interface IRuleMetadataValuesNormalizer
{
    bool TryNormalize(string? notes, IReadOnlyList<Guid>? tagIds, Guid? groupId, [NotNullWhen(true)] out RuleMetadataValues? values);
}
