using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Client.Features.Rules.Metadata;

/// <summary>
/// Makes group-deletion decisions against the confirmed snapshot and group catalog without performing I/O.
/// </summary>
internal static class RuleGroupDeletionPlanner
{
    public static RuleGroupCleanupCandidate? PlanSingleRuleCleanup(ListedFirewallRule rule, RuleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(rule.RuleId)
            || !snapshot.Metadata.TryGetValue(rule.RuleId, out RuleMetadata? metadata)
            || metadata.Group is null
            || snapshot.Rules.Count(candidate => string.Equals(candidate.RuleId, rule.RuleId, StringComparison.Ordinal)) != 1)
        {
            return null;
        }

        return new RuleGroupCleanupCandidate(metadata.Group.Id, rule.RuleId);
    }

    public static RuleGroup? FindSingleRuleCleanupGroup(RuleGroupCleanupCandidate candidate, IReadOnlyList<RuleGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(groups);

        return groups.SingleOrDefault(group => group.Id == candidate.GroupId
            && group.TemplateIds.Count == 0
            && group.RuleIds.Count == 1
            && string.Equals(group.RuleIds[0], candidate.RuleId, StringComparison.Ordinal));
    }

    public static RuleGroupDeletionPlan Create(RuleGroupManagementProjection projection, RuleSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.StoredMemberCount == 0)
        {
            throw new InvalidOperationException("A group with no live-rule memberships does not require a firewall-deletion plan.");
        }
        if (snapshot is null || !projection.MemberResolutionAvailable || projection.StaleMembershipCount != 0)
        {
            throw new InvalidOperationException("All group memberships must resolve against the current firewall snapshot before deleting the group and its rules.");
        }

        HashSet<string> memberRuleIds = projection.Group.RuleIds.ToHashSet(StringComparer.Ordinal);
        List<int> occurrenceIds = [];
        HashSet<string> resolvedRuleIds = new(StringComparer.Ordinal);
        for (int occurrenceId = 0; occurrenceId < snapshot.Rules.Count; occurrenceId++)
        {
            string? ruleId = snapshot.Rules[occurrenceId].RuleId;
            if (!string.IsNullOrWhiteSpace(ruleId) && memberRuleIds.Contains(ruleId))
            {
                occurrenceIds.Add(occurrenceId);
                resolvedRuleIds.Add(ruleId);
            }
        }
        if (occurrenceIds.Count == 0 || !resolvedRuleIds.SetEquals(memberRuleIds))
        {
            throw new InvalidOperationException("Every stored group membership must resolve against the current firewall snapshot before batch deletion.");
        }

        HashSet<int> previewOccurrenceIds = [.. projection.Members
            .SelectMany(static member => member.Occurrences)
            .Select(static occurrence => occurrence.OccurrenceId)];
        if (!previewOccurrenceIds.SetEquals(occurrenceIds))
        {
            throw new InvalidOperationException("The confirmed group member preview no longer matches the current firewall snapshot.");
        }

        RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
        return new RuleGroupDeletionPlan(
            projection.Group.Id,
            baseline,
            occurrenceIds.ToArray(),
            [.. memberRuleIds.Order(StringComparer.Ordinal)],
            [.. projection.Group.TemplateIds.Order()]);
    }

    public static bool MatchesConfirmedMembership(RuleGroupDeletionPlan plan, RuleGroup? currentGroup)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return currentGroup is not null
            && currentGroup.Id == plan.GroupId
            && currentGroup.RuleIds.ToHashSet(StringComparer.Ordinal).SetEquals(plan.ExpectedRuleIds)
            && currentGroup.TemplateIds.ToHashSet().SetEquals(plan.ExpectedTemplateIds);
    }

    public static bool CanDeleteEmptyGroup(RuleGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.RuleIds.Count == 0 && group.TemplateIds.Count == 0;
    }
}
