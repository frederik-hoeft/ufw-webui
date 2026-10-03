namespace Ufw.Web.Services.Rules;

/// <summary>
/// Indicates that the firewall replacement did not complete, so metadata reconciliation must not run.
/// </summary>
public sealed record RuleReplacementReconciliationNotRequired : RuleReplacementReconciliationPlan;
