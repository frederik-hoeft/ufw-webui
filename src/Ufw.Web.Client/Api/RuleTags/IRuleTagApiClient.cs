using Ufw.Web.Client.Api.RuleTags.Model;
namespace Ufw.Web.Client.Api.RuleTags;

public interface IRuleTagApiClient
{
    Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTagInventoryResponse> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken = default);

    Task<RuleTagInventoryResponse> UpdateAsync(Guid tagId, UpdateRuleTagRequest request, CancellationToken cancellationToken = default);

    Task<RuleTagInventoryResponse> DeleteAsync(Guid tagId, CancellationToken cancellationToken = default);
}
