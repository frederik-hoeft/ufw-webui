using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Intent;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal sealed class RuleOrderingService(IRuleApiClient ruleApiClient, IIntentContextApiClient intentContextApiClient, IIntentSigningService intentSigningService) : IRuleOrderingService
{
    public async Task<RuleReorderResponse> ApplyAsync(RuleListResponse baseline, IReadOnlyList<int> desiredOrder, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        RuleOrderPermutation permutation = RuleOrderPermutation.Create(desiredOrder, baseline.Rules.Count);

        IntentContextResponse context = await GetCompatibleIntentContextAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        ReorderRulesIntentRequest request = await intentSigningService.CreateReorderRulesRequestAsync(context.DeploymentId, baselineFingerprint, permutation.Occurrences, privateKey, cancellationToken);
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
}
