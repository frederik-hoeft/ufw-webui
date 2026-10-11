using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Authoring.Workflows;

internal interface IRuleCreationWorkflowService
{
    Task<RuleCreationAddResult> AddAsync(FirewallRuleSpecification rule, RuleMetadataChange metadata, string privateKey, CancellationToken cancellationToken = default);

    Task<RuleCreationInsertionResult> InsertAsync(
        RuleSnapshot baseline,
        OrderedRuleInsertionNavigationContext context,
        FirewallRuleSpecification rule,
        RuleMetadataChange metadata,
        string privateKey,
        CancellationToken cancellationToken = default);
}
