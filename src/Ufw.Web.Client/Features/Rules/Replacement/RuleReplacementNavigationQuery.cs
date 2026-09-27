namespace Ufw.Web.Client.Features.Rules.Replacement;

internal sealed record RuleReplacementNavigationQuery(string? BaselineFingerprint, string? TargetOccurrenceId, string? OriginalRuleId)
{
    public bool IsRequested => !string.IsNullOrWhiteSpace(BaselineFingerprint)
        || !string.IsNullOrWhiteSpace(TargetOccurrenceId)
        || !string.IsNullOrWhiteSpace(OriginalRuleId);
}
