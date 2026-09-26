using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Groups;

internal interface IRuleGroupFilterReconciler
{
    RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleGroup> currentGroups);
}
