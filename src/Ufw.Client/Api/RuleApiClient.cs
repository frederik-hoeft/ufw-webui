using System.Net.Http.Json;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Client.Serialization;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Web;

namespace Ufw.Client.Api;

internal sealed class RuleApiClient(HttpClient httpClient) : IRuleApiClient
{
    private const string RULES_PATH = "api/v1/rules";
    private static readonly Uri s_rulesUri = new(RULES_PATH, UriKind.Relative);
    private static readonly Uri s_ruleInsertUri = new("api/v1/rules/insert", UriKind.Relative);
    private static readonly Uri s_ruleOrderUri = new("api/v1/rules/order", UriKind.Relative);

    public async Task<RuleInventoryResponse> GetInventoryAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_rulesUri, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleInventoryResponse, cancellationToken);
    }

    public async Task<RuleMetadataMutationResponse> UpdateMetadataAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentNullException.ThrowIfNull(request);
        Uri uri = SimpleUriBuilder.Create(RULES_PATH)
            .AppendPath(Uri.EscapeDataString(ruleId))
            .AppendPath("metadata")
            .BuildUri(UriKind.Relative);
        using JsonContent content = JsonContent.Create(request, ClientJsonSerializerContext.Default.UpdateRuleMetadataRequest);
        using HttpResponseMessage response = await httpClient.PutAsync(uri, content, cancellationToken);
        return await response.ReadRequiredAsync(ClientJsonSerializerContext.Default.RuleMetadataMutationResponse, cancellationToken);
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

    public async Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(s_ruleInsertUri, request, MessageJsonSerializerContext.Default.InsertRuleRequest, cancellationToken);
        return await response.ReadTransactionResponseAsync(
            MessageJsonSerializerContext.Default.RuleInsertionResponse,
            static candidate => candidate.Outcome != RuleInsertionOutcome.Completed
                || candidate.FinalSnapshot is not null && candidate.InsertedRule is not null,
            cancellationToken);
    }

    public async Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync(s_ruleOrderUri, request, MessageJsonSerializerContext.Default.ReorderRulesRequest, cancellationToken);
        return await response.ReadTransactionResponseAsync(
            MessageJsonSerializerContext.Default.RuleReorderResponse,
            static candidate => candidate.Operations is not null
                && candidate.BlockedOperations is not null
                && candidate.PendingOperations is not null,
            cancellationToken);
    }
}
