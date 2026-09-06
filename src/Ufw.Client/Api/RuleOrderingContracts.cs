namespace Ufw.Client.Api;

/// <summary>
/// Provisional client-side ordering operation used only while staging the frontend mock flow.
/// </summary>
/// <remarks>
/// This is not an approved REST or signed-intent wire contract. Positions are one-based to match the
/// rule numbers presented to users.
/// </remarks>
public sealed record RuleMoveRequest(string RuleId, int TargetPosition);

/// <summary>
/// Provisional request sent when the user confirms a staged browser ordering preview.
/// </summary>
/// <remarks>
/// The production representation still requires explicit REST, intent-signing, stale-snapshot, and daemon design.
/// The frontend preserves the sequence of direct moves so the mock boundary can exercise an explicit confirmation
/// step without implying that this record is the eventual signed payload.
/// </remarks>
public sealed record RuleOrderingApplyRequest(IReadOnlyList<RuleMoveRequest> Moves);
