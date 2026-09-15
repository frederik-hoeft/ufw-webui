using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Shared.Web;

namespace Ufw.Client.RuleInsertion;

internal sealed class RuleInsertionNavigationService : IRuleInsertionNavigationService
{
    private const string CREATE_RULE_PATH = "/rules/create";

    public string BuildUri(RuleListResponse baseline, ListedFirewallRule anchor, RuleInsertionPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(anchor);
        if (!Enum.IsDefined(placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement));
        }

        int anchorOccurrenceId = FindOccurrenceId(baseline.Rules, anchor);
        if (anchorOccurrenceId < 0)
        {
            throw new InvalidOperationException("The selected insertion anchor is no longer present in the authoritative rule snapshot.");
        }
        if (!anchor.Parsed || anchor.Rule is null || anchor.Rule.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            throw new InvalidOperationException("Ordered insertion requires a parsed anchor with a concrete address family.");
        }
        if (anchor.Rule.AddressFamily == FirewallAddressFamily.IPv6 && !baseline.Configuration.IPv6Enabled)
        {
            throw new InvalidOperationException("Ordered insertion cannot target IPv6 while IPv6 support is disabled.");
        }

        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        return SimpleUriBuilder.Create(CREATE_RULE_PATH)
            .AppendQuery("baseline", fingerprint)
            .AppendQuery("anchor", anchorOccurrenceId)
            .AppendQuery("placement", FormatPlacement(placement))
            .Build();
    }

    public RuleInsertionNavigationResolution Resolve(RuleListResponse snapshot, RuleInsertionNavigationQuery query)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query);

        if (query.HasLegacyTarget
            || string.IsNullOrWhiteSpace(query.BaselineFingerprint)
            || string.IsNullOrWhiteSpace(query.AnchorOccurrenceId)
            || string.IsNullOrWhiteSpace(query.Placement))
        {
            return Failure(OrderedRuleInsertionContextError.Incomplete);
        }
        if (!FirewallRuleSnapshotFingerprint.IsValid(query.BaselineFingerprint))
        {
            return Failure(OrderedRuleInsertionContextError.InvalidFingerprint);
        }
        if (!int.TryParse(query.AnchorOccurrenceId, NumberStyles.None, CultureInfo.InvariantCulture, out int anchorOccurrenceId))
        {
            return Failure(OrderedRuleInsertionContextError.Incomplete);
        }
        if (!TryParsePlacement(query.Placement, out RuleInsertionPlacement placement))
        {
            return Failure(OrderedRuleInsertionContextError.InvalidPlacement);
        }
        if (!string.Equals(FirewallRuleSnapshotFingerprint.Compute(snapshot), query.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Failure(OrderedRuleInsertionContextError.StaleBaseline);
        }
        if (anchorOccurrenceId < 0 || anchorOccurrenceId >= snapshot.Rules.Count)
        {
            return Failure(OrderedRuleInsertionContextError.AnchorUnavailable);
        }

        ListedFirewallRule anchor = snapshot.Rules[anchorOccurrenceId];
        if (!anchor.Parsed || anchor.Rule is null || anchor.Rule.AddressFamily is not (FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6))
        {
            return Failure(OrderedRuleInsertionContextError.AnchorUnavailable);
        }
        if (anchor.Rule.AddressFamily == FirewallAddressFamily.IPv6 && !snapshot.Configuration.IPv6Enabled)
        {
            return Failure(OrderedRuleInsertionContextError.CapabilityUnavailable);
        }

        OrderedRuleInsertionNavigationContext context = new(
            query.BaselineFingerprint,
            anchorOccurrenceId,
            placement,
            anchor.Rule.AddressFamily,
            anchor);
        return new RuleInsertionNavigationResolution(context, OrderedRuleInsertionContextError.None);
    }

    private static int FindOccurrenceId(IReadOnlyList<ListedFirewallRule> rules, ListedFirewallRule anchor)
    {
        for (int index = 0; index < rules.Count; index++)
        {
            if (ReferenceEquals(rules[index], anchor))
            {
                return index;
            }
        }

        return -1;
    }

    private static RuleInsertionNavigationResolution Failure(OrderedRuleInsertionContextError error) => new(null, error);

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
