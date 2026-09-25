namespace Ufw.Web.Client.Features.Rules.Insertion;

internal sealed record RuleInsertionNavigationQuery(string? BaselineFingerprint, string? AnchorOccurrenceId, string? Placement, string? LegacyBeforeRuleId, string? LegacyAfterRuleId)
{
    public bool HasLegacyTarget => !string.IsNullOrWhiteSpace(LegacyBeforeRuleId) || !string.IsNullOrWhiteSpace(LegacyAfterRuleId);

    public bool IsRequested => HasLegacyTarget
        || !string.IsNullOrWhiteSpace(BaselineFingerprint)
        || !string.IsNullOrWhiteSpace(AnchorOccurrenceId)
        || !string.IsNullOrWhiteSpace(Placement);
}
