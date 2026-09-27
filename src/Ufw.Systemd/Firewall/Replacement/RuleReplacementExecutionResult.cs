using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed record RuleReplacementExecutionResult(
    RuleReplacementExecutionOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    ListedFirewallRule? ReplacementRule,
    RuleReplacementRecoveryStatus? RecoveryStatus,
    string? Diagnostic);
