namespace Ufw.Web.Services.Rules;

/// <summary>
/// Indicates that the firewall replacement completed but its signed request/response state could not be interpreted safely into metadata reconciliation facts.
/// </summary>
internal sealed record RuleReplacementReconciliationPreparationFailed : RuleReplacementReconciliationPlan;
