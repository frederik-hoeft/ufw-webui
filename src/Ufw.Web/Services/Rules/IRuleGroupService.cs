using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Services.Rules;

public interface IRuleGroupService
{
    Task<RuleGroupInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleGroupMutationResult> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken = default);

    Task<RuleGroupMutationResult> UpdateAsync(Guid publicId, UpdateRuleGroupRequest request, CancellationToken cancellationToken = default);

    Task<RuleGroupMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
