using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed class FirewallRuleReplacementExecutor(
    IFirewallRuleSnapshotReader snapshotReader,
    IFirewallRuleReplacementPreflightEvaluator preflightEvaluator,
    IFirewallRuleReplacementTransactionExecutor transactionExecutor) : IFirewallRuleReplacementExecutor
{
    public async Task<RuleReplacementExecutionResult> ExecuteAsync(ReplaceRulePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RuleReplacementContract.ValidatePayload(payload);

        RuleListResponse? baseline = await snapshotReader.ReadAsync(cancellationToken).OrDefaultAsync();
        if (baseline is null)
        {
            return Result(RuleReplacementExecutionOutcome.StateUncertain, null, diagnostic: "The authoritative firewall state could not be read before rule replacement.");
        }
        if (!string.Equals(FirewallRuleSnapshotFingerprint.Compute(baseline), payload.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Result(RuleReplacementExecutionOutcome.StaleBaseline, baseline, diagnostic: "The authoritative firewall state no longer matches the signed replacement baseline.");
        }

        return preflightEvaluator.Evaluate(baseline, payload) switch
        {
            RuleReplacementPreflightResult.Ready ready => await transactionExecutor.ExecuteAsync(baseline, ready.Preflight, cancellationToken),
            RuleReplacementPreflightResult.NoChange noChange => new RuleReplacementExecutionResult(RuleReplacementExecutionOutcome.Completed, baseline, noChange.Target, null, null),
            RuleReplacementPreflightResult.Rejected rejected => Result(rejected.Outcome, baseline, diagnostic: rejected.Diagnostic),
            _ => throw new InvalidOperationException("Unsupported replacement preflight result."),
        };
    }

    private static RuleReplacementExecutionResult Result(RuleReplacementExecutionOutcome outcome, RuleListResponse? finalSnapshot, string? diagnostic = null) =>
        new(outcome, finalSnapshot, null, null, diagnostic);
}
