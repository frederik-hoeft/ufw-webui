using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Api.RuleTemplates;

internal interface IRuleTemplateApiClient
{
    Task<RuleTemplateInventoryResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleTemplateInventoryResponse> CreateAsync(CreateRuleTemplateRequest request, CancellationToken cancellationToken = default);

    Task<RuleTemplateInventoryResponse> UpdateAsync(Guid templateId, UpdateRuleTemplateRequest request, CancellationToken cancellationToken = default);

    Task<RuleTemplateInventoryResponse> DeleteAsync(Guid templateId, CancellationToken cancellationToken = default);
}
