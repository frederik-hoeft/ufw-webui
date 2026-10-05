using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Web.Services.Rules;

/// <summary>
/// Combines the authoritative daemon firewall result with the Web-side metadata reconciliation plan derived from that result.
/// </summary>
public sealed record RuleReplacementExecutionResult(RuleReplacementResponse Firewall, RuleReplacementReconciliationPlan Reconciliation);
