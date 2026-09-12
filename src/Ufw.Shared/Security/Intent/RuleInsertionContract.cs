using Ufw.Shared.Firewall;

namespace Ufw.Shared.Security.Intent;

/// <summary>
/// Validates the state-independent and snapshot-local invariants of an ordered insertion intent.
/// </summary>
public static class RuleInsertionContract
{
    public static void ValidatePayload(InsertRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payload.Rule);

        if (!FirewallRuleSnapshotFingerprint.IsValid(payload.BaselineFingerprint))
        {
            throw new ArgumentException("A valid firewall snapshot fingerprint is required.", nameof(payload));
        }
        if (payload.AnchorOccurrenceId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), payload.AnchorOccurrenceId, "Anchor occurrence IDs are zero-based and cannot be negative.");
        }
        if (!Enum.IsDefined(payload.Placement))
        {
            throw new ArgumentOutOfRangeException(nameof(payload), payload.Placement, "Insertion placement is not supported.");
        }

        FirewallRuleSpecification normalized = RuleSpecificationNormalizer.Normalize(payload.Rule);
        if (normalized.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new ArgumentException("Ordered insertion requires a concrete IPv4 or IPv6 rule.", nameof(payload));
        }
    }

    public static ListedFirewallRule ResolveAnchor(IReadOnlyList<ListedFirewallRule> baselineRules, InsertRulePayload payload)
    {
        ArgumentNullException.ThrowIfNull(baselineRules);
        ValidatePayload(payload);

        if (payload.AnchorOccurrenceId >= baselineRules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), payload.AnchorOccurrenceId, "Anchor occurrence is outside the signed baseline.");
        }

        ListedFirewallRule anchor = baselineRules[payload.AnchorOccurrenceId];
        if (!anchor.Parsed || anchor.Rule is null)
        {
            throw new InvalidOperationException("Ordered insertion requires a parsed anchor rule with a concrete address family.");
        }

        FirewallRuleSpecification normalizedRule = RuleSpecificationNormalizer.Normalize(payload.Rule);
        FirewallAddressFamily anchorFamily = anchor.Rule.AddressFamily;
        if (anchorFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new InvalidOperationException("Ordered insertion requires an anchor with a concrete address family.");
        }
        if (normalizedRule.AddressFamily != anchorFamily)
        {
            throw new InvalidOperationException("Ordered insertion rule address family must match the anchor address family.");
        }

        return anchor;
    }
}
