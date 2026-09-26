namespace Ufw.Shared.Ipc.Model.Responses.Domain;

public sealed record RuleBatchDeleteResponse(
    RuleBatchDeleteOutcome Outcome,
    RuleListResponse? FinalSnapshot,
    IReadOnlyList<RuleBatchDeleteOperationResponse> Operations,
    IReadOnlyList<int> PendingOccurrenceIds,
    string? Diagnostic) : OkResponseBase;
