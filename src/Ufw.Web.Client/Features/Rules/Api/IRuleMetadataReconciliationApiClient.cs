namespace Ufw.Web.Client.Features.Rules.Api;

internal interface IRuleMetadataReconciliationApiClient
{
    Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<RuleMetadataReconciliationResponse> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken = default);
}
