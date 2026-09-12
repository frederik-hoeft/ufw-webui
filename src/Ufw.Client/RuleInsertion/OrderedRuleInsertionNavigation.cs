using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;

namespace Ufw.Client.RuleInsertion;

internal static class OrderedRuleInsertionNavigation
{
    private const string CREATE_RULE_PATH = "/rules/create";

    public static string BuildUri(
        RuleListResponse baseline,
        int anchorOccurrenceId,
        RuleInsertionPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (anchorOccurrenceId < 0 || anchorOccurrenceId >= baseline.Rules.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(anchorOccurrenceId));
        }
        if (!Enum.IsDefined(placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        ListedFirewallRule anchor = baseline.Rules[anchorOccurrenceId];
        if (!anchor.Parsed
            || anchor.Rule is null
            || anchor.Rule.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new InvalidOperationException("Ordered insertion requires a parsed anchor with a concrete address family.");
        }

        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        return $"{CREATE_RULE_PATH}?baseline={Uri.EscapeDataString(fingerprint)}"
            + $"&anchor={anchorOccurrenceId.ToString(CultureInfo.InvariantCulture)}"
            + $"&placement={FormatPlacement(placement)}";
    }

    public static bool TryResolve(
        RuleListResponse snapshot,
        string? baselineFingerprint,
        int? anchorOccurrenceId,
        string? placementValue,
        [NotNullWhen(true)] out OrderedRuleInsertionNavigationContext? context,
        out OrderedRuleInsertionContextError error)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        context = null;
        error = OrderedRuleInsertionContextError.None;

        if (string.IsNullOrWhiteSpace(baselineFingerprint)
            || anchorOccurrenceId is null
            || string.IsNullOrWhiteSpace(placementValue))
        {
            error = OrderedRuleInsertionContextError.Incomplete;
            return false;
        }
        if (!FirewallRuleSnapshotFingerprint.IsValid(baselineFingerprint))
        {
            error = OrderedRuleInsertionContextError.InvalidFingerprint;
            return false;
        }
        if (!TryParsePlacement(placementValue, out RuleInsertionPlacement placement))
        {
            error = OrderedRuleInsertionContextError.InvalidPlacement;
            return false;
        }
        if (!string.Equals(
            FirewallRuleSnapshotFingerprint.Compute(snapshot),
            baselineFingerprint,
            StringComparison.Ordinal))
        {
            error = OrderedRuleInsertionContextError.StaleBaseline;
            return false;
        }
        if (anchorOccurrenceId.Value < 0 || anchorOccurrenceId.Value >= snapshot.Rules.Count)
        {
            error = OrderedRuleInsertionContextError.AnchorUnavailable;
            return false;
        }

        ListedFirewallRule anchor = snapshot.Rules[anchorOccurrenceId.Value];
        if (!anchor.Parsed
            || anchor.Rule is null
            || anchor.Rule.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            error = OrderedRuleInsertionContextError.AnchorUnavailable;
            return false;
        }

        context = new OrderedRuleInsertionNavigationContext(
            baselineFingerprint,
            anchorOccurrenceId.Value,
            placement,
            anchor.Rule.AddressFamily,
            anchor);
        return true;
    }

    private static bool TryParsePlacement(string value, out RuleInsertionPlacement placement)
    {
        if (string.Equals(value, "before", StringComparison.OrdinalIgnoreCase))
        {
            placement = RuleInsertionPlacement.Before;
            return true;
        }
        if (string.Equals(value, "after", StringComparison.OrdinalIgnoreCase))
        {
            placement = RuleInsertionPlacement.After;
            return true;
        }

        placement = default;
        return false;
    }

    private static string FormatPlacement(RuleInsertionPlacement placement) => placement switch
    {
        RuleInsertionPlacement.Before => "before",
        RuleInsertionPlacement.After => "after",
        _ => throw new ArgumentOutOfRangeException(nameof(placement)),
    };
}
