using Ufw.Web.Model.V1.RuleTags;

namespace Ufw.Web.Services.Rules;

internal interface IRuleTagRepository
{
    Task<RuleTagInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> CreateAsync(string name, string color, CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> UpdateAsync(Guid publicId, string name, string color, CancellationToken cancellationToken = default);

    Task<RuleTagMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
