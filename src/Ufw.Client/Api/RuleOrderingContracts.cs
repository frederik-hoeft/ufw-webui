namespace Ufw.Client.Api;

/// <summary>
/// Provisional client-side ordering command used by the mock ASP boundary.
/// </summary>
/// <remarks>
/// This is not an approved REST or signed-intent wire contract. Positions are one-based to match the
/// rule numbers presented to users.
/// </remarks>
public sealed record RuleMoveRequest(string RuleId, int TargetPosition);
