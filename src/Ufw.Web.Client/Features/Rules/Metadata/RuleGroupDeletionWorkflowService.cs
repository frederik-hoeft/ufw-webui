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
        RuleGroupCleanupCandidate? candidate = RuleGroupDeletionPlanner.PlanSingleRuleCleanup(rule, snapshot);
        if (candidate is null)
        {
            return null;
        }

        IReadOnlyList<RuleGroup> groups = await groupCatalog.RefreshAsync(cancellationToken);
        return RuleGroupDeletionPlanner.FindSingleRuleCleanupGroup(candidate, groups);
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

        RuleGroupDeletionPlan plan = RuleGroupDeletionPlanner.Create(projection, snapshot);
        if (string.IsNullOrWhiteSpace(privateKey))
        {
            throw new ArgumentException("A private key is required to delete firewall rules.", nameof(privateKey));
        }

        IReadOnlyList<RuleGroup> groupsBeforeMutation = await groupCatalog.RefreshAsync(cancellationToken);
        RuleGroup? currentGroup = groupsBeforeMutation.SingleOrDefault(group => group.Id == plan.GroupId);
        if (!RuleGroupDeletionPlanner.MatchesConfirmedMembership(plan, currentGroup))
        {
            return new RuleGroupDeletionWorkflowResult(RuleGroupDeletionWorkflowOutcome.GroupChanged, groupsBeforeMutation);
        }

        RuleBatchDeleteResponse response = await ruleMutations.BatchDeleteRulesAsync(plan.Baseline, plan.OccurrenceIds, privateKey, cancellationToken);
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
        if (!RuleGroupDeletionPlanner.CanDeleteEmptyGroup(current))
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
