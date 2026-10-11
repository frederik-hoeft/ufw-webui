using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.Rules;

namespace Ufw.Web.Client.Features.Rules.Replacement;

internal sealed class RuleReplacementWorkflowService(
    IRuleMutationService mutations,
    IRuleMetadataMutationService metadataMutations,
    IClientErrorMapper errors) : IRuleReplacementWorkflowService
{
    public async Task<RuleEditWorkflowState> ReplaceAsync(
        RuleSnapshot baseline,
        RuleReplacementNavigationContext context,
        FirewallRuleSpecification replacementRule,
        RuleMetadataChange originalMetadata,
        RuleMetadataChange updatedMetadata,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(replacementRule);
        ArgumentNullException.ThrowIfNull(originalMetadata);
        ArgumentNullException.ThrowIfNull(updatedMetadata);

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(replacementRule);
        RuleReplacementMutationResponse response = await mutations.ReplaceRuleAsync(
            RuleSnapshotFactory.ToFirewallResponse(baseline), context.TargetOccurrenceId, context.OriginalRuleId, normalized, privateKey, cancellationToken);
        RuleEditWorkflowState state = RuleEditWorkflowState.Initial.ApplyReplacement(response);
        return state.CanApplyMetadataEdits
            ? await SaveMetadataAsync(state, originalMetadata, updatedMetadata, cancellationToken)
            : state;
    }

    public Task<RuleEditWorkflowState> RetryMetadataAsync(
        RuleEditWorkflowState state,
        RuleMetadataChange originalMetadata,
        RuleMetadataChange updatedMetadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.CanRetryMetadataSave)
        {
            throw new InvalidOperationException("Only a confirmed replacement with a failed metadata update can retry metadata persistence.");
        }
        return SaveMetadataAsync(state, originalMetadata, updatedMetadata, cancellationToken);
    }

    private async Task<RuleEditWorkflowState> SaveMetadataAsync(
        RuleEditWorkflowState state,
        RuleMetadataChange originalMetadata,
        RuleMetadataChange updatedMetadata,
        CancellationToken cancellationToken)
    {
        if (originalMetadata.HasSameValueAs(updatedMetadata))
        {
            return state.MetadataSaveCompleted();
        }

        string ruleId = state.ConfirmedRuleId
            ?? throw new InvalidOperationException("A confirmed replacement identity is required before updating metadata.");
        try
        {
            _ = await metadataMutations.UpdateAsync(ruleId, updatedMetadata, cancellationToken);
            return state.MetadataSaveCompleted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Keep the confirmed firewall outcome and permit retrying only the metadata write.
            return state.MetadataSaveFailed(errors.Describe(exception).Message);
        }
    }
}
