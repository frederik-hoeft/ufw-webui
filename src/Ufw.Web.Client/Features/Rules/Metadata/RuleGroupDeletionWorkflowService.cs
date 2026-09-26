using System.Net;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Features.Rules.Intent;

namespace Ufw.Web.Client.Features.Rules.Metadata;

internal sealed class RuleGroupDeletionWorkflowService(IRuleMutationService ruleMutations, IRuleGroupCatalogService groupCatalog) : IRuleGroupDeletionWorkflowService
{
    public async Task<RuleGroup?> GetSingleRuleCleanupCandidateAsync(ListedFirewallRule rule, RuleSnapshot snapshot, CancellationToken cancellationToken = default)
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

        IReadOnlyList<RuleGroup> groups = await groupCatalog.RefreshAsync(cancellationToken);
        return groups.SingleOrDefault(group => group.Id == metadata.Group.Id
            && group.RuleIds.Count == 1
            && string.Equals(group.RuleIds[0], rule.RuleId, StringComparison.Ordinal));
    }

    public async Task<RuleGroupDeletionWorkflowResult> DeleteAsync(
        RuleGroupManagementProjection projection,
        RuleSnapshot? snapshot,
        string? privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (projection.StoredMemberCount == 0)
        {
            RuleGroupCleanupResult emptyCleanup = await DeleteIfEmptyAsync(projection.Group.Id, cancellationToken);
            return new RuleGroupDeletionWorkflowResult(
                emptyCleanup.Deleted ? RuleGroupDeletionWorkflowOutcome.Deleted : RuleGroupDeletionWorkflowOutcome.GroupRetained,
                emptyCleanup.Groups);
        }

        if (snapshot is null || !projection.MemberResolutionAvailable || projection.StaleMembershipCount != 0)
        {
            throw new InvalidOperationException("All group memberships must resolve against the current firewall snapshot before deleting the group and its rules.");
        }
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("A private key is required to delete firewall rules.", nameof(privateKey));
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

        HashSet<int> previewOccurrenceIds = projection.Members
            .SelectMany(static member => member.Occurrences)
            .Select(static occurrence => occurrence.OccurrenceId)
            .ToHashSet();
        if (!previewOccurrenceIds.SetEquals(occurrenceIds))
        {
            throw new InvalidOperationException("The confirmed group member preview no longer matches the current firewall snapshot.");
        }

        IReadOnlyList<RuleGroup> groupsBeforeMutation = await groupCatalog.RefreshAsync(cancellationToken);
        RuleGroup? currentGroup = groupsBeforeMutation.SingleOrDefault(group => group.Id == projection.Group.Id);
        if (currentGroup is null || !currentGroup.RuleIds.ToHashSet(StringComparer.Ordinal).SetEquals(memberRuleIds))
        {
            return new RuleGroupDeletionWorkflowResult(RuleGroupDeletionWorkflowOutcome.GroupChanged, groupsBeforeMutation);
        }

        RuleListResponse baseline = new(snapshot.FirewallActive, snapshot.Rules, snapshot.Configuration);
        RuleBatchDeleteResponse response = await ruleMutations.BatchDeleteRulesAsync(baseline, occurrenceIds, privateKey, cancellationToken);
        try
        {
            IReadOnlyList<RuleGroup> groupsAfterBatch = await groupCatalog.RefreshAsync(cancellationToken);
            if (response.Outcome != RuleBatchDeleteOutcome.Completed)
            {
                return new RuleGroupDeletionWorkflowResult(RuleGroupDeletionWorkflowOutcome.BatchIncomplete, groupsAfterBatch, response);
            }

            RuleGroupCleanupResult cleanup = await DeleteIfEmptyCoreAsync(projection.Group.Id, groupsAfterBatch, cancellationToken);
            return new RuleGroupDeletionWorkflowResult(
                cleanup.Deleted ? RuleGroupDeletionWorkflowOutcome.Deleted : RuleGroupDeletionWorkflowOutcome.GroupRetained,
                cleanup.Groups,
                response);
        }
        catch (Exception exception) when (exception is ApiRequestException or ApiProtocolException)
        {
            RuleGroupDeletionWorkflowOutcome outcome = response.Outcome == RuleBatchDeleteOutcome.Completed
                ? RuleGroupDeletionWorkflowOutcome.GroupCleanupFailed
                : RuleGroupDeletionWorkflowOutcome.BatchIncomplete;
            return new RuleGroupDeletionWorkflowResult(outcome, groupCatalog.Current, response, exception.Message);
        }
    }

    public async Task<RuleGroupCleanupResult> DeleteIfEmptyAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<RuleGroup> groups = await groupCatalog.RefreshAsync(cancellationToken);
        return await DeleteIfEmptyCoreAsync(groupId, groups, cancellationToken);
    }

    private async Task<RuleGroupCleanupResult> DeleteIfEmptyCoreAsync(Guid groupId, IReadOnlyList<RuleGroup> groups, CancellationToken cancellationToken)
    {
        RuleGroup? current = groups.SingleOrDefault(group => group.Id == groupId);
        if (current is null)
        {
            return new RuleGroupCleanupResult(Deleted: true, groups);
        }
        if (current.RuleIds.Count != 0)
        {
            return new RuleGroupCleanupResult(Deleted: false, groups);
        }

        try
        {
            IReadOnlyList<RuleGroup> afterDelete = await groupCatalog.DeleteAsync(groupId, cancellationToken);
            return new RuleGroupCleanupResult(Deleted: true, afterDelete);
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            IReadOnlyList<RuleGroup> afterDelete = await groupCatalog.RefreshAsync(cancellationToken);
            return new RuleGroupCleanupResult(Deleted: true, afterDelete);
        }
        catch (ApiRequestException exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            IReadOnlyList<RuleGroup> afterConflict = await groupCatalog.RefreshAsync(cancellationToken);
            return new RuleGroupCleanupResult(Deleted: afterConflict.All(group => group.Id != groupId), afterConflict);
        }
    }
}
