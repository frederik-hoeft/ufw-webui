using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal interface IFirewallReorderPreflightEvaluator
{
    RuleReorderPreflightResult Evaluate(RuleListResponse baseline, IReadOnlyList<int> desiredOrder);

    IReadOnlyList<RuleReorderMove> CreateSafePendingPlan(RuleListResponse baseline, RuleListResponse? currentSnapshot, RuleReorderPreflight preflight);
}
