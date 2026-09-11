namespace Ufw.Client.Api;

/// <summary>
/// Provisional request sent when the user confirms a staged browser ordering preview.
/// </summary>
/// <remarks>
/// The production representation still requires explicit REST, intent-signing, stale-snapshot, and daemon design.
/// The frontend preserves the sequence of direct moves so the mock boundary can exercise an explicit confirmation
/// step without implying that this record is the eventual signed payload.
/// </remarks>
public sealed record RuleOrderingApplyRequest(IReadOnlyList<RuleMoveRequest> Moves);
