namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleBatchDeleteOperationResponse(int OccurrenceId, string? RuleId, RuleBatchDeleteOperationOutcome Outcome, string? Diagnostic);
