using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Replacement;

/// <summary>
/// Validates a signed replacement request against an authoritative baseline and selects the safe replacement transaction strategy without mutating firewall state.
/// </summary>
internal interface IFirewallRuleReplacementPreflightEvaluator
{
    /// <summary>
    /// Evaluates snapshot-local target identity, duplicate constraints, capabilities, interfaces, and no-op state before replacement execution begins.
    /// </summary>
    RuleReplacementPreflightResult Evaluate(RuleListResponse baseline, ReplaceRulePayload payload);
}
