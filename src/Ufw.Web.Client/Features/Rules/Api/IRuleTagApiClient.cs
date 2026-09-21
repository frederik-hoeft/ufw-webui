namespace Ufw.Web.Client.Features.Rules.Api;

public interface IRuleTagApiClient
{
    Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTagInventoryResponse> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken = default);

    Task<RuleTagInventoryResponse> UpdateAsync(Guid tagId, UpdateRuleTagRequest request, CancellationToken cancellationToken = default);

    Task<RuleTagInventoryResponse> DeleteAsync(Guid tagId, CancellationToken cancellationToken = default);
}
