namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleReorderOperationResponse(
    RuleReorderMoveResponse Move,
    RuleReorderOperationOutcome Outcome,
    string? Diagnostic);
