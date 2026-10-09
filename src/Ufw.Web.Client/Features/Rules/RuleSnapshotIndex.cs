using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Client.Features.Rules;

/// <summary>
/// Resolves occurrence coordinates, family-local positions, and semantic multiplicity within one ordered authoritative snapshot.
/// Occurrence IDs are meaningful only together with the snapshot fingerprint; they are not durable rule identities.
/// </summary>
internal sealed class RuleSnapshotIndex
{
    private readonly IReadOnlyList<ListedFirewallRule> _rules;
    private readonly FirewallAddressFamily[] _families;
    private readonly int[] _familyPositions;
    private readonly Dictionary<FirewallAddressFamily, int> _familyCounts = [];
    private readonly Dictionary<string, int> _identityCounts = new(StringComparer.Ordinal);

    public RuleSnapshotIndex(IReadOnlyList<ListedFirewallRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
        _families = new FirewallAddressFamily[rules.Count];
        _familyPositions = new int[rules.Count];

        for (int occurrenceId = 0; occurrenceId < rules.Count; occurrenceId++)
        {
            ListedFirewallRule rule = rules[occurrenceId];
            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(rule);
            _families[occurrenceId] = family;
            int position = _familyCounts.GetValueOrDefault(family) + 1;
            _familyCounts[family] = position;
            _familyPositions[occurrenceId] = position;

            if (!string.IsNullOrWhiteSpace(rule.RuleId))
            {
                _identityCounts[rule.RuleId] = _identityCounts.GetValueOrDefault(rule.RuleId) + 1;
            }
        }
    }

    public int Count => _rules.Count;

    public bool TryGet(int occurrenceId, [NotNullWhen(true)] out ListedFirewallRule? rule)
    {
        if (occurrenceId < 0 || occurrenceId >= _rules.Count)
        {
            rule = null;
            return false;
        }

        rule = _rules[occurrenceId];
        return true;
    }

    public bool TryGet(int occurrenceId, string expectedRuleId, [NotNullWhen(true)] out ListedFirewallRule? rule)
    {
        if (TryGet(occurrenceId, out rule) && string.Equals(rule.RuleId, expectedRuleId, StringComparison.Ordinal))
        {
            return true;
        }

        rule = null;
        return false;
    }

    public FirewallAddressFamily GetFamily(int occurrenceId) => _families[occurrenceId];

    public int GetFamilyPosition(int occurrenceId) => _familyPositions[occurrenceId];

    public int GetFamilyCount(FirewallAddressFamily family) => _familyCounts.GetValueOrDefault(family);

    public int GetIdentityMultiplicity(string ruleId) => _identityCounts.GetValueOrDefault(ruleId);

    public bool HasUniqueIdentity(string ruleId) => GetIdentityMultiplicity(ruleId) == 1;
}
