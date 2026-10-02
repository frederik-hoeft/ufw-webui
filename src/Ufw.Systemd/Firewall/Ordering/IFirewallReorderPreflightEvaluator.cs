using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

/// <summary>
/// Validates a requested reorder against an authoritative baseline and produces only plans that respect immutable occurrences and family-ordering constraints.
/// </summary>
internal interface IFirewallReorderPreflightEvaluator
{
    /// <summary>
    /// Evaluates the requested complete occurrence ordering and returns either an executable reorder preflight or an expected precondition rejection.
    /// </summary>
    RuleReorderPreflightResult Evaluate(RuleListResponse baseline, IReadOnlyList<int> desiredOrder);

    /// <summary>
    /// Reconstructs the subset of the accepted plan that can still be applied safely after an interrupted move, or an empty plan when the observed state cannot be mapped unambiguously.
    /// </summary>
    IReadOnlyList<RuleReorderMove> CreateSafePendingPlan(RuleListResponse baseline, RuleListResponse? currentSnapshot, RuleReorderPreflight preflight);
}
