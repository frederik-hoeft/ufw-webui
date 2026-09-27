using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Features.Rules.Templates;

internal sealed record RuleDisableWorkflowResult
{
    private RuleDisableWorkflowResult(
        RuleDisableWorkflowOutcome outcome,
        string templateName,
        RuleMutationResponse? deleteResponse,
        ClientError? error)
    {
        Outcome = outcome;
        TemplateName = templateName;
        DeleteResponse = deleteResponse;
        Error = error;
    }

    public RuleDisableWorkflowOutcome Outcome { get; }

    public string TemplateName { get; }

    public RuleMutationResponse? DeleteResponse { get; }

    public ClientError? Error { get; }

    public bool TemplatePersistenceConfirmed => Outcome != RuleDisableWorkflowOutcome.TemplatePersistenceNotConfirmed;

    public bool Completed => Outcome == RuleDisableWorkflowOutcome.Completed;

    public static RuleDisableWorkflowResult Success(string templateName, RuleMutationResponse deleteResponse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(deleteResponse);
        return new RuleDisableWorkflowResult(RuleDisableWorkflowOutcome.Completed, templateName, deleteResponse, error: null);
    }

    public static RuleDisableWorkflowResult TemplatePersistenceNotConfirmed(string templateName, ClientError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(error);
        return new RuleDisableWorkflowResult(RuleDisableWorkflowOutcome.TemplatePersistenceNotConfirmed, templateName, deleteResponse: null, error);
    }

    public static RuleDisableWorkflowResult FirewallDeleteNotConfirmed(string templateName, ClientError error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);
        ArgumentNullException.ThrowIfNull(error);
        return new RuleDisableWorkflowResult(RuleDisableWorkflowOutcome.FirewallDeleteNotConfirmed, templateName, deleteResponse: null, error);
    }
}
