namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleReorderMoveResponse(int OccurrenceId, int TargetIndex, int? BeforeOccurrenceId);
