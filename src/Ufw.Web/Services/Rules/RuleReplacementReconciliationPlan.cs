namespace Ufw.Web.Services.Rules;

/// <summary>
/// Describes whether metadata reconciliation is required and whether the daemon replacement result could be interpreted into reconciliation facts.
/// </summary>
public abstract record RuleReplacementReconciliationPlan
{
    private protected RuleReplacementReconciliationPlan() { }
}
