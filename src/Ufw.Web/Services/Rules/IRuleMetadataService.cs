using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Services.Rules;

public interface IRuleMetadataService
{
    Task<RuleMetadataUpdateResult> UpdateAsync(string ruleId, UpdateRuleMetadataRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles persisted metadata from trusted rule-identity facts derived after a completed firewall replacement.
    /// </summary>
    Task<RuleReplacementMetadataReconciliationOutcome> ReconcileReplacementAsync(RuleReplacementReconciliationFacts facts, CancellationToken cancellationToken = default);

    Task RemoveForDeletedRuleAsync(string ruleId, CancellationToken cancellationToken = default);

    Task ReconcileBatchDeleteAsync(RuleBatchDeleteResponse response, CancellationToken cancellationToken = default);
}
