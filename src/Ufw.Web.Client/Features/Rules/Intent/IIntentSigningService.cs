using Ufw.Shared.Firewall;
using Ufw.Web.Model.V1.Rules.Intent;

namespace Ufw.Web.Client.Features.Rules.Intent;

public interface IIntentSigningService
{
    Task<AddRuleIntentRequest> CreateAddRuleRequestAsync(string deploymentId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default);

    Task<DeleteRuleIntentRequest> CreateDeleteRuleRequestAsync(string deploymentId, string ruleId, FirewallRuleSpecification rule, string privateKey, CancellationToken cancellationToken = default);

    Task<BatchDeleteRulesIntentRequest> CreateBatchDeleteRulesRequestAsync(
        string deploymentId,
        string baselineFingerprint,
        IReadOnlyList<int> occurrenceIds,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<InsertRuleIntentRequest> CreateInsertRuleRequestAsync(
        string deploymentId,
        string baselineFingerprint,
        int anchorOccurrenceId,
        RuleInsertionPlacement placement,
        FirewallRuleSpecification rule,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<ReplaceRuleIntentRequest> CreateReplaceRuleRequestAsync(
        string deploymentId,
        string baselineFingerprint,
        int targetOccurrenceId,
        string originalRuleId,
        FirewallRuleSpecification replacementRule,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<ReorderRulesIntentRequest> CreateReorderRulesRequestAsync(
        string deploymentId,
        string baselineFingerprint,
        IReadOnlyList<int> desiredOrder,
        string privateKey,
        CancellationToken cancellationToken = default);
}
