using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal interface IFirewallReorderMoveExecutor
{
    Task<RuleReorderMoveExecutionResult> ExecuteAsync(
        RuleReorderMove move,
        RuleListResponse baseline,
        IReadOnlyList<int> preMoveOrder,
        FirewallRuleSpecification specification,
        CancellationToken cancellationToken);
}
