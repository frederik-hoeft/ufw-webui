using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

internal interface IRuleTagRepository
{
    Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> CreateAsync(string name, string color, CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> UpdateAsync(Guid publicId, string name, string color, CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
