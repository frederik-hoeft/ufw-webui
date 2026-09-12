using Ufw.Shared.Firewall;

namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleInsertionResponse(
    RuleInsertionOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    ListedFirewallRule? InsertedRule,
    string? Diagnostic) : OkResponseBase;
