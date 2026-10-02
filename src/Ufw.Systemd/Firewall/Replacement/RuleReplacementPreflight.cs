using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed record RuleReplacementPreflight(
    int TargetOccurrenceId,
    FirewallRuleSpecification Replacement,
    string ReplacementId,
    RuleReplacementTransactionKind TransactionKind);
