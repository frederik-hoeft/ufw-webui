using Ufw.Web.Model.V1.RuleMetadata;

namespace Ufw.Web.Client.Api.RuleMetadata;

internal interface IRuleMetadataReconciliationApiClient
{
    Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataReconciliationResponse> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken = default);
}
