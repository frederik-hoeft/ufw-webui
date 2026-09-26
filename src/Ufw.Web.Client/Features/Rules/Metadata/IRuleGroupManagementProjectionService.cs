using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal interface IRuleGroupManagementProjectionService
{
    IReadOnlyList<RuleGroupManagementProjection> Create(IReadOnlyList<RuleGroup> groups, RuleSnapshot? ruleSnapshot);
}
