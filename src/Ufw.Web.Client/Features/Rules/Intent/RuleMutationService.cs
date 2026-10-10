using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Model.V1.Rules;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Features.Rules.Intent;

internal sealed class RuleMutationService(IRuleApiClient ruleApiClient, ICompatibleIntentContextProvider intentContextProvider, IIntentSigningService intentSigningService) : IRuleMutationService
{
    public async Task<RuleMutationResponse> AddRuleAsync(FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default)
    {
        string deploymentId = await intentContextProvider.GetDeploymentIdAsync(cancellationToken);
        AddRuleIntentRequest request = await intentSigningService.CreateAddRuleRequestAsync(deploymentId, rule, privateKey, cancellationToken);
        return await ruleApiClient.AddRuleAsync(request, cancellationToken);
    }

    public async Task<RuleMutationResponse> DeleteRuleAsync(ListedFirewallRule rule, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!rule.Parsed || rule.Rule is null || string.IsNullOrWhiteSpace(rule.RuleId))
        {
            throw new InvalidOperationException("Only parsed rules with a stable rule ID can be deleted.");
        }

        string deploymentId = await intentContextProvider.GetDeploymentIdAsync(cancellationToken);
        DeleteRuleIntentRequest request = await intentSigningService.CreateDeleteRuleRequestAsync(deploymentId, rule.RuleId, rule.Rule, privateKey, cancellationToken);
        return await ruleApiClient.DeleteRuleAsync(request, cancellationToken);
    }

    public async Task<RuleBatchDeleteResponse> BatchDeleteRulesAsync(
        RuleListResponse baseline,
        IReadOnlyList<int> occurrenceIds,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(occurrenceIds);
        string deploymentId = await intentContextProvider.GetDeploymentIdAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        BatchDeleteRulesIntentRequest request = await intentSigningService.CreateBatchDeleteRulesRequestAsync(
            deploymentId,
            baselineFingerprint,
            occurrenceIds,
            privateKey,
            cancellationToken);
        return await ruleApiClient.BatchDeleteRulesAsync(request, cancellationToken);
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

        string deploymentId = await intentContextProvider.GetDeploymentIdAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        InsertRuleIntentRequest request = await intentSigningService.CreateInsertRuleRequestAsync(
            deploymentId,
            baselineFingerprint,
            anchorOccurrenceId,
            placement,
            rule,
            privateKey,
            cancellationToken);
        return await ruleApiClient.InsertRuleAsync(request, cancellationToken);
    }

    public async Task<RuleReplacementMutationResponse> ReplaceRuleAsync(
        RuleListResponse baseline,
        int targetOccurrenceId,
        string originalRuleId,
        FirewallRuleSpecification replacementRule,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(replacementRule);

        string deploymentId = await intentContextProvider.GetDeploymentIdAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        ReplaceRuleIntentRequest request = await intentSigningService.CreateReplaceRuleRequestAsync(
            deploymentId,
            baselineFingerprint,
            targetOccurrenceId,
            originalRuleId,
            replacementRule,
            privateKey,
            cancellationToken);
        return await ruleApiClient.ReplaceRuleAsync(request, cancellationToken);
    }
}
