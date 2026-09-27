using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Web;

namespace Ufw.Web.Client.Features.Rules.Replacement;

internal sealed class RuleReplacementNavigationService : IRuleReplacementNavigationService
{
    private const string EDIT_RULE_PATH = "/rules/edit";

    public string BuildUri(RuleListResponse baseline, ListedFirewallRule target)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(target);

        int targetOccurrenceId = FindOccurrenceId(baseline.Rules, target);
        if (targetOccurrenceId < 0)
        {
            throw new InvalidOperationException("The selected rule is no longer present in the authoritative rule snapshot.");
        }
        if (!TryGetEditableTarget(target, out FirewallRuleSpecification? targetRule, out string? originalRuleId))
        {
            throw new InvalidOperationException("Rule editing requires a parsed rule with a concrete address family and semantic identity.");
        }
        if (!HasUniqueIdentity(baseline.Rules, originalRuleId))
        {
            throw new InvalidOperationException("Rule editing is unavailable while duplicate rules with the same semantic identity exist.");
        }
        if (targetRule.AddressFamily == FirewallAddressFamily.IPv6 && !baseline.Configuration.IPv6Enabled)
        {
            throw new InvalidOperationException("Rule editing cannot target IPv6 while IPv6 support is disabled.");
        }

        string fingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline);
        return SimpleUriBuilder.Create(EDIT_RULE_PATH)
            .AppendQuery("baseline", fingerprint)
            .AppendQuery("target", targetOccurrenceId)
            .AppendQuery("ruleId", originalRuleId)
            .Build();
    }

    public RuleReplacementNavigationResolution Resolve(RuleListResponse snapshot, RuleReplacementNavigationQuery query)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.BaselineFingerprint)
            || string.IsNullOrWhiteSpace(query.TargetOccurrenceId)
            || string.IsNullOrWhiteSpace(query.OriginalRuleId))
        {
            return Failure(RuleReplacementContextError.Incomplete);
        }
        if (!FirewallRuleSnapshotFingerprint.IsValid(query.BaselineFingerprint))
        {
            return Failure(RuleReplacementContextError.InvalidFingerprint);
        }
        if (!int.TryParse(query.TargetOccurrenceId, NumberStyles.None, CultureInfo.InvariantCulture, out int targetOccurrenceId))
        {
            return Failure(RuleReplacementContextError.Incomplete);
        }
        if (!string.Equals(FirewallRuleSnapshotFingerprint.Compute(snapshot), query.BaselineFingerprint, StringComparison.Ordinal))
        {
            return Failure(RuleReplacementContextError.StaleBaseline);
        }
        if (targetOccurrenceId < 0 || targetOccurrenceId >= snapshot.Rules.Count)
        {
            return Failure(RuleReplacementContextError.TargetUnavailable);
        }

        ListedFirewallRule target = snapshot.Rules[targetOccurrenceId];
        if (!TryGetEditableTarget(target, out FirewallRuleSpecification? targetRule, out string? originalRuleId))
        {
            return Failure(RuleReplacementContextError.TargetUnavailable);
        }
        if (!string.Equals(originalRuleId, query.OriginalRuleId, StringComparison.Ordinal))
        {
            return Failure(RuleReplacementContextError.TargetMismatch);
        }
        if (!HasUniqueIdentity(snapshot.Rules, originalRuleId))
        {
            return Failure(RuleReplacementContextError.DuplicateIdentity);
        }
        if (targetRule.AddressFamily == FirewallAddressFamily.IPv6 && !snapshot.Configuration.IPv6Enabled)
        {
            return Failure(RuleReplacementContextError.CapabilityUnavailable);
        }

        RuleReplacementNavigationContext context = new(
            query.BaselineFingerprint,
            targetOccurrenceId,
            GetFamilyPosition(snapshot.Rules, targetOccurrenceId, targetRule.AddressFamily),
            originalRuleId,
            targetRule.AddressFamily,
            target);
        return new RuleReplacementNavigationResolution(context, RuleReplacementContextError.None);
    }

    private static bool TryGetEditableTarget(
        ListedFirewallRule target,
        [NotNullWhen(true)] out FirewallRuleSpecification? targetRule,
        [NotNullWhen(true)] out string? originalRuleId)
    {
        targetRule = target.Rule;
        originalRuleId = target.RuleId;
        return target.Parsed
            && targetRule is not null
            && targetRule.AddressFamily is FirewallAddressFamily.IPv4 or FirewallAddressFamily.IPv6
            && !string.IsNullOrWhiteSpace(originalRuleId);
    }

    private static bool HasUniqueIdentity(IReadOnlyList<ListedFirewallRule> rules, string ruleId)
    {
        int matches = 0;
        foreach (ListedFirewallRule rule in rules)
        {
            if (!string.Equals(rule.RuleId, ruleId, StringComparison.Ordinal))
            {
                continue;
            }

            matches++;
            if (matches > 1)
            {
                return false;
            }
        }

        return matches == 1;
    }

    private static int GetFamilyPosition(IReadOnlyList<ListedFirewallRule> rules, int occurrenceId, FirewallAddressFamily family)
    {
        int familyPosition = 0;
        for (int index = 0; index <= occurrenceId; index++)
        {
            if (ListedFirewallRuleFamily.GetObservedFamily(rules[index]) == family)
            {
                familyPosition++;
            }
        }

        return familyPosition;
    }

    private static int FindOccurrenceId(IReadOnlyList<ListedFirewallRule> rules, ListedFirewallRule target)
    {
        for (int index = 0; index < rules.Count; index++)
        {
            if (ReferenceEquals(rules[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private static RuleReplacementNavigationResolution Failure(RuleReplacementContextError error) => new(null, error);
}
