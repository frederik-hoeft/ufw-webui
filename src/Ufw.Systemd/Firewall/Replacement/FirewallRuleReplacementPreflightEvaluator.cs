using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Firewall.Replacement;

internal sealed class FirewallRuleReplacementPreflightEvaluator(
    IFirewallRuleInterfaceValidator interfaceValidator,
    IFirewallRuleCapabilityValidator capabilityValidator) : IFirewallRuleReplacementPreflightEvaluator
{
    public RuleReplacementPreflightResult Evaluate(RuleListResponse baseline, ReplaceRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(payload);
        RuleReplacementContract.ValidatePayload(payload);

        if (!RuleReplacementContract.TryResolveTarget(baseline.Rules, payload, out ListedFirewallRule? target, out string? targetDiagnostic))
        {
            return new RuleReplacementPreflightResult.Rejected(RuleReplacementExecutionOutcome.PreconditionFailed, targetDiagnostic);
        }

        FirewallRuleSpecification replacement = RuleSpecificationNormalizer.Normalize(payload.ReplacementRule);
        FirewallRuleSpecification original = RuleSpecificationNormalizer.Normalize(target.Rule!);
        string replacementId = RuleIdentity.Compute(replacement);
        bool sameIdentity = string.Equals(replacementId, payload.OriginalRuleId, StringComparison.Ordinal);
        int originalMultiplicity = baseline.Rules.Count(rule => string.Equals(rule.RuleId, payload.OriginalRuleId, StringComparison.Ordinal));

        if (sameIdentity && originalMultiplicity != 1)
        {
            return new RuleReplacementPreflightResult.Rejected(
                RuleReplacementExecutionOutcome.PreconditionFailed,
                "The target semantic rule identity occurs more than once. UFW cannot safely apply an occurrence-specific existing-rule update while duplicates are present.");
        }
        if (sameIdentity && FirewallRuleStateComparer.Equals(original, replacement))
        {
            return new RuleReplacementPreflightResult.NoChange(target);
        }
        if (!sameIdentity && baseline.Rules.Any(rule => string.Equals(rule.RuleId, replacementId, StringComparison.Ordinal)))
        {
            return new RuleReplacementPreflightResult.Rejected(RuleReplacementExecutionOutcome.PreconditionFailed, "The replacement would duplicate an existing semantic firewall rule.");
        }

        IResponsePayload? capabilityError = capabilityValidator.Validate(replacement, baseline.Configuration);
        if (capabilityError is not null)
        {
            return new RuleReplacementPreflightResult.Rejected(RuleReplacementExecutionOutcome.PreconditionFailed, GetResponseDiagnostic(capabilityError));
        }

        IResponsePayload? interfaceError = interfaceValidator.Validate(replacement);
        if (interfaceError is not null)
        {
            RuleReplacementExecutionOutcome outcome = interfaceError is ModelValidationErrorResponse
                ? RuleReplacementExecutionOutcome.PreconditionFailed
                : RuleReplacementExecutionOutcome.StateUncertain;
            return new RuleReplacementPreflightResult.Rejected(outcome, GetResponseDiagnostic(interfaceError));
        }

        RuleReplacementTransactionKind transactionKind = sameIdentity
            ? RuleReplacementTransactionKind.UpdateExisting
            : RuleReplacementTransactionKind.InsertThenDelete;
        return new RuleReplacementPreflightResult.Ready(new RuleReplacementPreflight(payload.TargetOccurrenceId, replacement, replacementId, transactionKind));
    }

    private static string GetResponseDiagnostic(IResponsePayload response) => response switch
    {
        ErrorResponse error => error.Message ?? "Rule replacement precondition validation failed.",
        _ => "Rule replacement precondition validation failed.",
    };
}
