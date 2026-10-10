using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Tags;

internal sealed class RuleTagFilterReconciler : IRuleTagFilterReconciler
{
    public RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleTag> currentItems) =>
        CatalogRuleFilterReconciler.Reconcile<TagRuleFilter, RuleTag>(
            query, currentItems, static filter => filter.Tag, static item => item.Id, static item => new TagRuleFilter(item));
}
