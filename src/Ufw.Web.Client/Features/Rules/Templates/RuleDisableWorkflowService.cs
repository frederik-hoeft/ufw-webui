using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Features.Rules.Intent;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed class RuleDisableWorkflowService(
    IRuleTemplateCatalogService templateCatalog,
    IRuleTemplateAuthoringService templateAuthoring,
    IRuleMutationService ruleMutations,
    IClientErrorMapper clientErrors) : IRuleDisableWorkflowService
{
    public async Task<RuleDisableWorkflowResult> DisableAsync(
        RuleRowProjection row,
        string templateName,
        string? templateDescription,
        string privateKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        if (!row.CanMutate || !row.Rule.Parsed || row.Rule.Rule is null || string.IsNullOrWhiteSpace(row.Rule.RuleId))
        {
            throw new InvalidOperationException("Only uniquely mutable parsed rules can be disabled.");
        }

        RuleTemplateDefinition definition = templateAuthoring.CreateDefinition(templateName, templateDescription, row.Rule.Rule, row.Metadata);
        try
        {
            _ = await templateCatalog.CreateAsync(definition, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return RuleDisableWorkflowResult.TemplatePersistenceNotConfirmed(definition.Name, clientErrors.Describe(exception));
        }

        try
        {
            RuleMutationResponse response = await ruleMutations.DeleteRuleAsync(row.Rule, privateKey, cancellationToken);
            return RuleDisableWorkflowResult.Success(definition.Name, response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return RuleDisableWorkflowResult.FirewallDeleteNotConfirmed(definition.Name, clientErrors.Describe(exception));
        }
    }
}
