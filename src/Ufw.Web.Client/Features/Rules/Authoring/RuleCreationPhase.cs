namespace Ufw.Web.Client.Features.Rules.Authoring;

internal enum RuleCreationPhase
{
    Initializing,
    Ready,
    Validating,
    Submitting,
    AwaitingConfirmation,
    Completed,
}
