using Ufw.Client.Intent;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.Api;

internal sealed class RuleOrderingService(
    IRuleApiClient ruleApiClient,
    IIntentContextApiClient intentContextApiClient,
    IIntentSigningService intentSigningService) : IRuleOrderingService
{
    public async Task<RuleReorderResponse> ApplyAsync(
        RuleListResponse baseline,
        IReadOnlyList<int> desiredOrder,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        ValidatePermutation(desiredOrder, baseline.Rules.Count);

        IntentContextResponse context = await GetCompatibleIntentContextAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        ReorderRulesRequest request = await intentSigningService.CreateReorderRulesRequestAsync(
            context.DeploymentId,
            baselineFingerprint,
            desiredOrder,
            privateKey,
            cancellationToken);
        return await ruleApiClient.ReorderRulesAsync(request, cancellationToken);
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

    private static void ValidatePermutation(IReadOnlyList<int> desiredOrder, int ruleCount)
    {
        if (desiredOrder.Count != ruleCount)
        {
            throw new ArgumentException("The desired ordering must contain every baseline occurrence exactly once.", nameof(desiredOrder));
        }

        bool[] seen = new bool[ruleCount];
        foreach (int occurrenceId in desiredOrder)
        {
            if (occurrenceId < 0 || occurrenceId >= ruleCount || seen[occurrenceId])
            {
                throw new ArgumentException("The desired ordering must be a permutation of the baseline occurrences.", nameof(desiredOrder));
            }

            seen[occurrenceId] = true;
        }
    }
}
