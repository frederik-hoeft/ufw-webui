using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api.Rules;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Features.Rules.Ordering;

internal sealed class RuleOrderingService(IRuleApiClient ruleApiClient, ICompatibleIntentContextProvider intentContextProvider, IIntentSigningService intentSigningService) : IRuleOrderingService
{
    public async Task<RuleReorderResponse> ApplyAsync(RuleListResponse baseline, IReadOnlyList<int> desiredOrder, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(desiredOrder);
        RuleOrderPermutation permutation = RuleOrderPermutation.Create(desiredOrder, baseline.Rules.Count);

        string deploymentId = await intentContextProvider.GetDeploymentIdAsync(cancellationToken);
        string baselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        ReorderRulesIntentRequest request = await intentSigningService.CreateReorderRulesRequestAsync(deploymentId, baselineFingerprint, permutation.Occurrences, privateKey, cancellationToken);
        return await ruleApiClient.ReorderRulesAsync(request, cancellationToken);
    }
}
