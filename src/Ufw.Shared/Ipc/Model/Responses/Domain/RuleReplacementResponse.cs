using Ufw.Shared.Firewall;

namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleReplacementResponse(
    RuleReplacementOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    ListedFirewallRule? ReplacementRule,
    RuleReplacementRecoveryOutcome? RecoveryOutcome,
    string? Diagnostic) : OkResponseBase;
