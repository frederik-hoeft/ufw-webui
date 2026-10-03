namespace Ufw.Web.Services.Rules;

/// <summary>
/// Indicates that a completed firewall replacement was interpreted successfully and metadata reconciliation can proceed.
/// </summary>
public sealed record RuleReplacementReconciliationReady(RuleReplacementReconciliationFacts Facts) : RuleReplacementReconciliationPlan;
