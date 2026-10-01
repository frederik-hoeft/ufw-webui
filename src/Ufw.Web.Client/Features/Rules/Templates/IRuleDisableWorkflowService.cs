namespace Ufw.Web.Client.Features.Rules.Templates;

internal interface IRuleDisableWorkflowService
{
    Task<RuleDisableWorkflowResult> DisableAsync(
        RuleRowProjection row,
        string? templateName,
        string? templateDescription,
        string privateKey,
        CancellationToken cancellationToken = default);
}
