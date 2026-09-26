using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Features.Rules.Filtering.Groups;

internal sealed class RuleGroupFilterReconciler : IRuleGroupFilterReconciler
{
    public RuleQuery Reconcile(RuleQuery query, IReadOnlyList<RuleGroup> currentGroups)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(currentGroups);

        Dictionary<Guid, RuleGroup> groupsById = currentGroups.ToDictionary(static group => group.Id);
        List<RuleFilter> filters = new(query.Filters.Count);
        foreach (RuleFilter filter in query.Filters)
        {
            if (filter is not GroupRuleFilter groupFilter)
            {
                filters.Add(filter);
                continue;
            }

            if (groupsById.TryGetValue(groupFilter.Group.Id, out RuleGroup? currentGroup))
            {
                filters.Add(new GroupRuleFilter(currentGroup));
            }
        }

        return filters.Count == 0 ? RuleQuery.Empty : new RuleQuery(filters);
    }
}
