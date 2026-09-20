using Ufw.Web.Api.V1.Models.Rules;

namespace Ufw.Web.Services.Rules;

public interface IRuleMetadataReconciliationService
{
    Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataReconciliationResponse> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken = default);
}
