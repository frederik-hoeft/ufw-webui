using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed record ReorderRecoveryJournalEntry(
    int FormatVersion,
    FirewallRuleSpecification Rule,
    int OriginalFamilyPosition,
    int ExpectedMultiplicity,
    RuleRecoveryAnchor? PreviousAnchor,
    RuleRecoveryAnchor? NextAnchor)
{
    public const int CURRENT_FORMAT_VERSION = 2;
}
