using System.Net.Http.Json;
using Ufw.Client.Intent;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Serialization.Json;

namespace Ufw.Client.Api;

internal sealed class UfwApiClient(HttpClient httpClient, IIntentSigningService intentSigningService) : IUfwApiClient
{
    private static readonly Uri s_rulesUri = new("api/v1/rules", UriKind.Relative);
    private static readonly Uri s_intentContextUri = new("api/v1/intent/context", UriKind.Relative);

    public async Task<RuleListResponse> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_rulesUri, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleListResponse, cancellationToken);
    }

    public async Task<RuleMutationResponse> AddRuleAsync(
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        IntentContextResponse context = await GetIntentContextAsync(cancellationToken);
        AddRuleRequest request = await intentSigningService.CreateAddRuleRequestAsync(
            context.DeploymentId,
            rule,
            privateKey,
            cancellationToken);
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            s_rulesUri,
            request,
            MessageJsonSerializerContext.Default.AddRuleRequest,
            cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleMutationResponse, cancellationToken);
    }

    public async Task<RuleMutationResponse> DeleteRuleAsync(
        ListedFirewallRule rule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
        {
            throw new InvalidOperationException("Only parsed rules with a stable rule ID can be deleted.");
        }

        IntentContextResponse context = await GetIntentContextAsync(cancellationToken);
        DeleteRuleRequest request = await intentSigningService.CreateDeleteRuleRequestAsync(
            context.DeploymentId,
            rule.RuleId,
            rule.Rule,
            privateKey,
            cancellationToken);
        using HttpRequestMessage httpRequest = new(HttpMethod.Delete, s_rulesUri)
        {
            Content = JsonContent.Create(request, MessageJsonSerializerContext.Default.DeleteRuleRequest),
        };
        using HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);
        return await response.ReadRequiredAsync(MessageJsonSerializerContext.Default.RuleMutationResponse, cancellationToken);
    }

    private async Task<IntentContextResponse> GetIntentContextAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(s_intentContextUri, cancellationToken);
        IntentContextResponse context = await response.ReadRequiredAsync(
            MessageJsonSerializerContext.Default.IntentContextResponse,
            cancellationToken);
        if (context.ProtocolVersion != Ufw.Ipc.Shared.Security.Intent.IntentProtocol.VERSION)
        {
            throw new ApiProtocolException(
                $"Intent protocol mismatch. Client supports version "
                + $"{Ufw.Ipc.Shared.Security.Intent.IntentProtocol.VERSION}, server reports {context.ProtocolVersion}.");
        }

        return context;
    }
}
