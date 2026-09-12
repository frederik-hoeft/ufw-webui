namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleReorderResponse(
    RuleReorderOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    RuleReorderOperationResponse[] Operations,
    RuleReorderMoveResponse[] BlockedOperations,
    RuleReorderMoveResponse[] PendingOperations,
    string? Diagnostic) : OkResponseBase;
