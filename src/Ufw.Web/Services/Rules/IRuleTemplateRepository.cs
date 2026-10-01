using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Services.Rules;

internal interface IRuleTemplateRepository
{
    Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTemplateMutationResult> CreateAsync(RuleTemplateValues values, CancellationToken cancellationToken = default);

    Task<RuleTemplateMutationResult> UpdateAsync(Guid publicId, RuleTemplateValues values, CancellationToken cancellationToken = default);

    Task<RuleTemplateMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
