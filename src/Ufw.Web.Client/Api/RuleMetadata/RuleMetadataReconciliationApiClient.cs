using System.Net.Http.Json;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.RuleMetadata.Model;

namespace Ufw.Web.Client.Api.RuleMetadata;

internal sealed class RuleMetadataReconciliationApiClient(HttpClient httpClient) : IRuleMetadataReconciliationApiClient
{
    private static readonly Uri s_reconciliationUri = new("api/v1/rule-metadata/reconciliation", UriKind.Relative);
    private static readonly Uri s_cleanupUri = new("api/v1/rule-metadata/reconciliation/cleanup", UriKind.Relative);

    public async Task<RuleMetadataReconciliationResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_reconciliationUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleMetadataReconciliationResponse, cancellationToken);
    }

    public async Task<RuleMetadataReconciliationResponse> CleanupAsync(CleanupRuleMetadataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.CleanupRuleMetadataRequest);
        using HttpResponseMessage response = await httpClient.PostAsync(s_cleanupUri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleMetadataReconciliationResponse, cancellationToken);
    }
}
