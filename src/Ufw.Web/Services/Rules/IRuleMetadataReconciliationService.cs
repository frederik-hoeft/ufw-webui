using Ufw.Web.Model.V1.RuleMetadata;

namespace Ufw.Web.Services.Rules;

public interface IRuleMetadataReconciliationService
{
    Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataReconciliationResponse> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken = default);
}
