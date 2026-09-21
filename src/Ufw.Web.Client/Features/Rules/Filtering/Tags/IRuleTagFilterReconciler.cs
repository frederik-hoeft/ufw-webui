using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Tags;

internal interface IRuleTagFilterReconciler
{
    RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleTag> currentTags);
}
