using System.Diagnostics.CodeAnalysis;
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

    public static bool TryResolveAnchor(IReadOnlyList<ListedFirewallRule> baselineRules, InsertRulePayload payload, [NotNullWhen(true)] out ListedFirewallRule? anchor, [NotNullWhen(false)] out string? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(baselineRules);
        ValidatePayload(payload);

        if (payload.AnchorOccurrenceId >= baselineRules.Count)
        {
            return Fail("Anchor occurrence is outside the signed baseline.", out anchor, out diagnostic);
        }

        anchor = baselineRules[payload.AnchorOccurrenceId];
        if (!anchor.Parsed || anchor.Rule is null)
        {
            return Fail("Ordered insertion requires a parsed anchor rule with a concrete address family.", out anchor, out diagnostic);
        }

        FirewallRuleSpecification normalizedRule = RuleSpecificationNormalizer.Normalize(payload.Rule);
        FirewallAddressFamily anchorFamily = anchor.Rule.AddressFamily;
        if (anchorFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            return Fail("Ordered insertion requires an anchor with a concrete address family.", out anchor, out diagnostic);
        }
        if (normalizedRule.AddressFamily != anchorFamily)
        {
            return Fail("Ordered insertion rule address family must match the anchor address family.", out anchor, out diagnostic);
        }

        diagnostic = null;
        return true;
    }

    private static bool Fail(string message, out ListedFirewallRule? anchor, out string? diagnostic)
    {
        anchor = null;
        diagnostic = message;
        return false;
    }
}
