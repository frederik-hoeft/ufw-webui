namespace Ufw.Client.Api;

/// <summary>
/// Browser-local ordering gesture. The occurrence ID is the zero-based row position in the authoritative preview baseline.
/// </summary>
public sealed record RuleMoveRequest(int OccurrenceId, int TargetPosition);
