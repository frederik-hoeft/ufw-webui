using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

public interface IRuleTagService
{
    Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> CreateAsync(CreateRuleTagRequest request, CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> UpdateAsync(Guid publicId, UpdateRuleTagRequest request, CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
