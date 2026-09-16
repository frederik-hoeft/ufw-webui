using Ufw.Client.Rules.Metadata;

namespace Ufw.Client.Rules.Filtering.Tags;

internal interface IRuleTagFilterReconciler
{
    RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleTag> currentTags);
}
