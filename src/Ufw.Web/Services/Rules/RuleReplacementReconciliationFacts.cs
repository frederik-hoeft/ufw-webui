namespace Ufw.Web.Services.Rules;

/// <summary>
/// Describes the authoritative rule-identity facts needed to reconcile metadata after a completed firewall replacement.
/// </summary>
public sealed record RuleReplacementReconciliationFacts(string OriginalRuleId, string ReplacementRuleId, bool OriginalRuleStillLive);
