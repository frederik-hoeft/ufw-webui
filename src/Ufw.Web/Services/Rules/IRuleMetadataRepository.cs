using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

internal interface IRuleMetadataRepository
{
    Task<IReadOnlyList<RuleMetadataItem>> GetForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default);

    Task<RuleMetadataSaveResult> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default);
}
