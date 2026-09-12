using System.Net.Http.Json;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;

namespace Ufw.Client.Api;

internal sealed class RuleApiClient(HttpClient httpClient) : IRuleApiClient
{
    private static readonly Uri s_rulesUri = new("api/v1/rules", UriKind.Relative);
    private static readonly Uri s_ruleOrderUri = new("api/v1/rules/order", UriKind.Relative);

    public async Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_rulesUri, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleListResponse, cancellationToken);
    }

    public async Task<RuleMutationResponse> AddRuleAsync(AddRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(s_rulesUri, request, MessageJsonSerializerContext.Default.AddRuleRequest, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleMutationResponse, cancellationToken);
    }

    public async Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpRequestMessage httpRequest = new(HttpMethod.Delete, s_rulesUri)
        {
            Content = JsonContent.Create(request, MessageJsonSerializerContext.Default.DeleteRuleRequest),
        };
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleMutationResponse, cancellationToken);
    }

    public async Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync(
            s_ruleOrderUri,
            request,
            MessageJsonSerializerContext.Default.ReorderRulesRequest,
            cancellationToken);
        return await response.ReadTransactionResponseAsync(
            MessageJsonSerializerContext.Default.RuleReorderResponse,
            static candidate => candidate.Operations is not null
                && candidate.BlockedOperations is not null
                && candidate.PendingOperations is not null,
            cancellationToken);
    }
}
