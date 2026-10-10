using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Insertion;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules.Authoring.Workflows;

internal sealed class RuleCreationWorkflowService(
    IRuleMutationService mutations,
    IRuleMetadataMutationService metadataMutations,
    IRuleMutationReconciliationService reconciliation,
    IClientErrorMapper errors) : IRuleCreationWorkflowService
{
    public async Task<RuleCreationAddResult> AddAsync(FirewallRuleSpecification rule, RuleMetadataChange metadata, string privateKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(metadata);
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(rule);
        string requestedIdentity = reconciliation.GetRequestedIdentity(normalized);
        RuleMutationResponse response = await mutations.AddRuleAsync(normalized, privateKey, cancellationToken);
        string confirmedIdentity = reconciliation.GetMutationIdentity(response, requestedIdentity);
        ClientError? metadataError = await TrySaveMetadataAsync(response.Rule.RuleId, metadata, cancellationToken);
        return new RuleCreationAddResult(response, confirmedIdentity, metadataError);
    }

    public async Task<RuleCreationInsertionResult> InsertAsync(
        RuleSnapshot baseline,
        OrderedRuleInsertionNavigationContext context,
        FirewallRuleSpecification rule,
        RuleMetadataChange metadata,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(metadata);
        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(rule);
        RuleInsertionResponse response = await mutations.InsertRuleAsync(
            RuleSnapshotFactory.ToFirewallResponse(baseline), context.AnchorOccurrenceId, context.Placement, normalized, privateKey, cancellationToken);
        ClientError? metadataError = response.Outcome == RuleInsertionOutcome.Completed
            ? await TrySaveMetadataAsync(response.InsertedRule?.RuleId, metadata, cancellationToken)
            : null;
        return new RuleCreationInsertionResult(response, metadataError);
    }

    private async Task<ClientError?> TrySaveMetadataAsync(string? ruleId, RuleMetadataChange metadata, CancellationToken cancellationToken)
    {
        if (metadata.IsEmpty || string.IsNullOrWhiteSpace(ruleId))
        {
            return null;
        }

        try
        {
            _ = await metadataMutations.UpdateAsync(ruleId, metadata, cancellationToken);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The firewall operation has already completed. Do not let a metadata failure turn it into an unknown firewall outcome.
            return errors.Describe(exception);
        }
    }
}
