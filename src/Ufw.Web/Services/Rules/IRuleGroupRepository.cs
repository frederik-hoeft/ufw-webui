using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Services.Rules;

internal interface IRuleGroupRepository
{
    Task<RuleGroupInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleGroupMutationResult> CreateAsync(string name, string? comment, CancellationToken cancellationToken = default);

    Task<RuleGroupMutationResult> UpdateAsync(Guid publicId, string name, string? comment, CancellationToken cancellationToken = default);

    Task<RuleGroupMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
