using Ufw.Client.Intent;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Api;

internal sealed class RuleMutationService(IRuleApiClient ruleApiClient, IIntentContextApiClient intentContextApiClient, IIntentSigningService intentSigningService) : IRuleMutationService
{
    public async Task<RuleMutationResponse> AddRuleAsync(FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default)
    {
        IntentContextResponse context = await GetCompatibleIntentContextAsync(cancellationToken);
        AddRuleRequest request = await intentSigningService.CreateAddRuleRequestAsync(context.DeploymentId, rule, privateKey, cancellationToken);
        return await ruleApiClient.AddRuleAsync(request, cancellationToken);
    }

    public async Task<RuleMutationResponse> DeleteRuleAsync(ListedFirewallRule rule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
        {
            throw new InvalidOperationException("Only parsed rules with a stable rule ID can be deleted.");
        }

        IntentContextResponse context = await GetCompatibleIntentContextAsync(cancellationToken);
        DeleteRuleRequest request = await intentSigningService.CreateDeleteRuleRequestAsync(context.DeploymentId, rule.RuleId, rule.Rule, privateKey, cancellationToken);
        return await ruleApiClient.DeleteRuleAsync(request, cancellationToken);
    }

    public async Task<RuleInsertionResponse> InsertRuleAsync(
        RuleListResponse baseline,
        int anchorOccurrenceId,
        RuleInsertionPlacement placement,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(rule);

        IntentContextResponse context = await GetCompatibleIntentContextAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        InsertRuleRequest request = await intentSigningService.CreateInsertRuleRequestAsync(
            context.DeploymentId,
            baselineFingerprint,
            anchorOccurrenceId,
            placement,
            rule,
            privateKey,
            cancellationToken);
        return await ruleApiClient.InsertRuleAsync(request, cancellationToken);
    }

    private async Task<IntentContextResponse> GetCompatibleIntentContextAsync(CancellationToken cancellationToken)
    {
        IntentContextResponse context = await intentContextApiClient.GetAsync(cancellationToken);
        if (context.ProtocolVersion != IntentProtocol.VERSION)
        {
            throw new ApiProtocolException($"Intent protocol mismatch. Client supports version {IntentProtocol.VERSION}, server reports {context.ProtocolVersion}.");
        }

        return context;
    }
}
