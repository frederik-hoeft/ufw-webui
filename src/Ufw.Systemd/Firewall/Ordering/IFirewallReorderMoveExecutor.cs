using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// Executes one planned reorder move as a recoverable delete/reinsert transaction against the authoritative firewall state.
/// </summary>
internal interface IFirewallReorderMoveExecutor
{
    /// <summary>
    /// Executes <paramref name="move"/>, reconciling every mutation against authoritative state and recovering the removed rule before returning an interrupted result when possible.
    /// </summary>
    Task<RuleReorderMoveExecutionResult> ExecuteAsync(
        RuleReorderMove move,
        RuleListResponse baseline,
        IReadOnlyList<int> preMoveOrder,
        FirewallRuleSpecification specification,
        CancellationToken cancellationToken);
}
