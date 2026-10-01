using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Services.Rules;

public interface IRuleTemplateService
{
    Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTemplateMutationResult> CreateAsync(CreateRuleTemplateRequest request, CancellationToken cancellationToken = default);

    Task<RuleTemplateMutationResult> UpdateAsync(Guid publicId, UpdateRuleTemplateRequest request, CancellationToken cancellationToken = default);

    Task<RuleTemplateMutationResult> DeleteAsync(Guid publicId, CancellationToken cancellationToken = default);
}
