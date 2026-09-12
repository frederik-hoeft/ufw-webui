using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Insertion;

internal sealed record RuleInsertionExecutionResult(
    RuleInsertionExecutionOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    ListedFirewallRule? InsertedRule,
    string? Diagnostic);
