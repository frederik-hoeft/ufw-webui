using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Services.Rules;

internal interface IRuleMetadataRepository
{
    Task<IReadOnlyList<RuleMetadataItem>> GetForRuleIdsAsync(IReadOnlyCollection<string> ruleIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleMetadataItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataSaveResult> SaveAsync(string ruleId, RuleMetadataValues values, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default);

    Task<int> DeleteUnmatchedAsync(IReadOnlyCollection<Guid> metadataIds, IReadOnlyCollection<string> liveRuleIds, CancellationToken cancellationToken = default);
}
