using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

/// <summary>
/// Validates the state-independent and snapshot-local invariants of a rule replacement intent.
/// </summary>
public static class RuleReplacementContract
{
    public static void ValidatePayload(ReplaceRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payload.ReplacementRule);

        if (!FirewallRuleSnapshotFingerprint.IsValid(payload.BaselineFingerprint))
        {
            throw new ArgumentException("A valid firewall snapshot fingerprint is required.", nameof(payload));
        }
        if (payload.TargetOccurrenceId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), payload.TargetOccurrenceId, "Target occurrence IDs are zero-based and cannot be negative.");
        }
        if (!RuleIdentity.IsValid(payload.OriginalRuleId))
        {
            throw new ArgumentException("A valid original semantic rule identity is required.", nameof(payload));
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.ReplacementRule);
        if (normalized.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new ArgumentException("Rule replacement requires a concrete IPv4 or IPv6 replacement rule.", nameof(payload));
        }
    }

    public static bool TryResolveTarget(
        IReadOnlyList<ListedFirewallRule> baselineRules,
        ReplaceRulePayload payload,
        [NotNullWhen(true)] out ListedFirewallRule? target,
        [NotNullWhen(false)] out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(baselineRules);
        ValidatePayload(payload);

        if (payload.TargetOccurrenceId >= baselineRules.Count)
        {
            target = null;
            diagnostic = "Target occurrence is outside the signed baseline.";
            return false;
        }

        target = baselineRules[payload.TargetOccurrenceId];
        if (!target.Parsed || target.Rule is null || string.IsNullOrWhiteSpace(target.RuleId))
        {
            target = null;
            diagnostic = "Rule replacement requires a parsed target rule with a stable semantic identity and concrete address family.";
            return false;
        }
        if (!string.Equals(target.RuleId, payload.OriginalRuleId, StringComparison.Ordinal))
        {
            target = null;
            diagnostic = "Rule replacement target identity does not match the signed original rule identity.";
            return false;
        }

        FirewallRuleSpecification normalizedReplacement = RuleSpecificationNormalizer.Normalize(payload.ReplacementRule);
        FirewallAddressFamily targetFamily = target.Rule.AddressFamily;
        if (targetFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            target = null;
            diagnostic = "Rule replacement requires a target with a concrete address family.";
            return false;
        }
        if (normalizedReplacement.AddressFamily != targetFamily)
        {
            target = null;
            diagnostic = "Replacement rule address family must match the target rule address family.";
            return false;
        }

        diagnostic = null;
        return true;
    }
}
