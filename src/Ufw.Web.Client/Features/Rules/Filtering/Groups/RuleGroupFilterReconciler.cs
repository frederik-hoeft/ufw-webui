using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Groups;

internal sealed class RuleGroupFilterReconciler : IRuleGroupFilterReconciler
{
    public RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleGroup> currentItems) =>
        CatalogRuleFilterReconciler.Reconcile<GroupRuleFilter, RuleGroup>(
            query, currentItems, static filter => filter.Group, static item => item.Id, static item => new GroupRuleFilter(item));
}
