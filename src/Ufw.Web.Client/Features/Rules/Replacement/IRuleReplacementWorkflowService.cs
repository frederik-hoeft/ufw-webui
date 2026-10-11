using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Replacement;

internal interface IRuleReplacementWorkflowService
{
    Task<RuleEditWorkflowState> ReplaceAsync(
        RuleSnapshot baseline,
        RuleReplacementNavigationContext context,
        FirewallRuleSpecification replacementRule,
        RuleMetadataChange originalMetadata,
        RuleMetadataChange updatedMetadata,
        string privateKey,
        CancellationToken cancellationToken = default);

    Task<RuleEditWorkflowState> RetryMetadataAsync(
        RuleEditWorkflowState state,
        RuleMetadataChange originalMetadata,
        RuleMetadataChange updatedMetadata,
        CancellationToken cancellationToken = default);
}
