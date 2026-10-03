using System.Net.Http.Json;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Web;
using Ufw.Web.Client.Api;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Api.Rules;

internal sealed class RuleApiClient(HttpClient httpClient) : IRuleApiClient
{
    private const string RULES_PATH = "api/v1/rules";
    private static readonly Uri s_rulesUri = new(RULES_PATH, UriKind.Relative);
    private static readonly Uri s_ruleInsertUri = new("api/v1/rules/insert", UriKind.Relative);
    private static readonly Uri s_ruleBatchDeleteUri = new("api/v1/rules/batch", UriKind.Relative);
    private static readonly Uri s_ruleOrderUri = new("api/v1/rules/order", UriKind.Relative);
    private static readonly Uri s_ruleReplaceUri = new("api/v1/rules/replace", UriKind.Relative);

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

    public async Task<RuleMutationResponse> AddRuleAsync(AddRuleIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(s_rulesUri, request, ClientJsonSerializerContext.Default.AddRuleIntentRequest, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleMutationResponse, cancellationToken);
    }

    public async Task<RuleMutationResponse> DeleteRuleAsync(DeleteRuleIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpRequestMessage httpRequest = new(HttpMethod.Delete, s_rulesUri)
        {
            Content = JsonContent.Create(request, ClientJsonSerializerContext.Default.DeleteRuleIntentRequest),
        };
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleMutationResponse, cancellationToken);
    }

    public async Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(BatchDeleteRulesIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpRequestMessage httpRequest = new(HttpMethod.Delete, s_ruleBatchDeleteUri)
        {
            Content = JsonContent.Create(request, ClientJsonSerializerContext.Default.BatchDeleteRulesIntentRequest),
        };
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        return await response.ReadTransactionResponseAsync(
            MessageJsonSerializerContext.Default.RuleBatchDeleteResponse,
            static candidate => candidate.Operations is not null
                && candidate.PendingOccurrenceIds is not null
                && (candidate.Outcome == RuleBatchDeleteOutcome.StateUncertain || candidate.FinalSnapshot is not null),
            cancellationToken);
    }

    public async Task<RuleInsertionResponse> InsertRuleAsync(InsertRuleIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(s_ruleInsertUri, request, ClientJsonSerializerContext.Default.InsertRuleIntentRequest, cancellationToken);
        return await response.ReadTransactionResponseAsync(
            MessageJsonSerializerContext.Default.RuleInsertionResponse,
            static candidate => candidate.Outcome != RuleInsertionOutcome.Completed
                || candidate.FinalSnapshot is not null && candidate.InsertedRule is not null,
            cancellationToken);
    }

    public async Task<RuleReplacementMutationResponse> ReplaceRuleAsync(ReplaceRuleIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync(s_ruleReplaceUri, request, ClientJsonSerializerContext.Default.ReplaceRuleIntentRequest, cancellationToken);
        return await response.ReadTransactionResponseAsync(
            ClientJsonSerializerContext.Default.RuleReplacementMutationResponse,
            IsValidReplacementResponse,
            cancellationToken);
    }

    private static bool IsValidReplacementResponse(RuleReplacementMutationResponse response)
    {
        if (response.Firewall is null
            || response.Firewall.Outcome == RuleReplacementOutcome.Completed && (response.Firewall.FinalSnapshot is null || response.Firewall.ReplacementRule is null))
        {
            return false;
        }

        if (response.Firewall.Outcome != RuleReplacementOutcome.Completed)
        {
            return response.MetadataReconciliation == RuleReplacementMetadataReconciliationOutcome.NotAttempted
                && response.MetadataDiagnostic is null;
        }

        return response.MetadataReconciliation switch
        {
            RuleReplacementMetadataReconciliationOutcome.Completed => response.MetadataDiagnostic is null,
            RuleReplacementMetadataReconciliationOutcome.Failed => !string.IsNullOrWhiteSpace(response.MetadataDiagnostic),
            _ => false,
        };
    }

    public async Task<RuleReorderResponse> ReorderRulesAsync(ReorderRulesIntentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync(s_ruleOrderUri, request, ClientJsonSerializerContext.Default.ReorderRulesIntentRequest, cancellationToken);
        return await response.ReadTransactionResponseAsync(
            MessageJsonSerializerContext.Default.RuleReorderResponse,
            static candidate => candidate.Operations is not null
                && candidate.BlockedOperations is not null
                && candidate.PendingOperations is not null,
            cancellationToken);
    }
}
