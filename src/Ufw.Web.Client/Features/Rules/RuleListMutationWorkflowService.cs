using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Ordering;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules;

internal sealed class RuleListMutationWorkflowService(
    IRuleMetadataMutationService metadataMutations,
    IRuleMutationService ruleMutations,
    IRuleGroupDeletionWorkflowService groupDeletion,
    IRuleOrderingService ordering,
    IClientErrorMapper clientErrors,
    TimeProvider timeProvider) : IRuleListMutationWorkflowService
{
    public async Task<RuleInventoryState> UpdateMetadataAsync(RuleInventoryState state, string ruleId, RuleMetadataChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        RuleMetadataMutationResponse response = await metadataMutations.UpdateAsync(ruleId, change, cancellationToken);
        return state.MoveNext(new RuleInventoryTransition.MetadataMutationCompleted(ruleId, response));
    }

    public Task<RuleGroup?> GetSingleRuleCleanupCandidateAsync(ListedFirewallRule rule, RuleSnapshot snapshot, CancellationToken cancellationToken = default) =>
        groupDeletion.GetSingleRuleCleanupCandidateAsync(rule, snapshot, cancellationToken);

    public async Task<RuleListDeletionResult> DeleteAsync(ListedFirewallRule rule, string privateKey, RuleGroup? groupToDelete, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await ruleMutations.DeleteRuleAsync(rule, privateKey, cancellationToken);
        if (groupToDelete is null)
        {
            return new RuleListDeletionResult(null, null);
        }

        try
        {
            RuleGroupCleanupResult cleanup = await groupDeletion.DeleteIfEmptyAsync(groupToDelete.Id, cancellationToken);
            return new RuleListDeletionResult(cleanup, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (clientErrors.CanDescribe(exception))
        {
            return new RuleListDeletionResult(null, clientErrors.Describe(exception));
        }
    }

    public async Task<RuleListOrderingResult> ApplyOrderingAsync(
        RuleInventoryState state,
        RuleOrderingPreview preview,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(preview);
        RuleSnapshot snapshot = state.Snapshot ?? throw new InvalidOperationException("Cannot reorder an unloaded firewall snapshot.");
        RuleListResponse baseline = RuleSnapshotFactory.ToFirewallResponse(snapshot);
        int[] desiredOrder = [.. preview.DesiredOrder];
        RuleReorderResponse response = await ordering.ApplyAsync(baseline, desiredOrder, privateKey, cancellationToken);
        RuleInventoryState updated = state.MoveNext(new RuleInventoryTransition.ReorderCompleted(response, timeProvider.GetUtcNow()));
        return new RuleListOrderingResult(updated, response, baseline.Rules.ToArray(), desiredOrder);
    }
}
