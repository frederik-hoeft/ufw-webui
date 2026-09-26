using Ufw.Web.Model.V1.RuleGroups;

namespace Ufw.Web.Client.Api.RuleGroups;

public interface IRuleGroupApiClient
{
    Task<RuleGroupInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleGroupInventoryResponse> CreateAsync(CreateRuleGroupRequest request, CancellationToken cancellationToken = default);

    Task<RuleGroupInventoryResponse> UpdateAsync(Guid groupId, UpdateRuleGroupRequest request, CancellationToken cancellationToken = default);

    Task<RuleGroupInventoryResponse> DeleteAsync(Guid groupId, CancellationToken cancellationToken = default);
}
