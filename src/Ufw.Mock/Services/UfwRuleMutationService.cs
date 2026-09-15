using Ufw.Mock.Cli;
using Ufw.Mock.Rules;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwRuleMutationService
{
    public IReadOnlyList<UfwRuleMutationResult> Add(UfwMockState state, ParsedRuleRequest request, bool ipv6Enabled) =>
        Mutate(state, request, ipv6Enabled, RulePlacement.Append, null);

    public IReadOnlyList<UfwRuleMutationResult> Insert(UfwMockState state, ParsedRuleRequest request, int insertNumber, bool ipv6Enabled) =>
        Mutate(state, request, ipv6Enabled, RulePlacement.Insert, insertNumber);

    public IReadOnlyList<UfwRuleMutationResult> Prepend(UfwMockState state, ParsedRuleRequest request, bool ipv6Enabled) =>
        Mutate(state, request, ipv6Enabled, RulePlacement.Prepend, null);

    public List<UfwMockRule> Delete(UfwMockState state, ParsedRuleRequest request, bool ipv6Enabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<UfwMockRule> targets = request.Materialize(ipv6Enabled);
        List<UfwMockRule> matches = [];
        foreach (UfwMockRule target in targets)
        {
            UfwMockRule? match = state.Rules.FirstOrDefault(existing => UfwRuleComparer.SemanticallyEqual(existing, target));
            if (match is not null)
            {
                matches.Add(match);
            }
        }
        if (matches.Count == 0)
        {
            throw new UfwCliException("Could not delete non-existent rule");
        }

        foreach (UfwMockRule match in matches)
        {
            state.Rules.Remove(match);
        }
        return matches;
    }

    public UfwMockRule DeleteByNumber(UfwMockState state, int displayNumber, bool ipv6Enabled)
    {
        if (displayNumber <= 0)
        {
            throw new UfwCliException("Rule numbers are one-based.");
        }

        IReadOnlyList<UfwMockRule> observableRules = UfwRuleVisibility.GetObservableRules(state, ipv6Enabled);
        if (displayNumber > observableRules.Count)
        {
            throw new UfwCliException("Could not delete non-existent rule");
        }

        UfwMockRule rule = observableRules[displayNumber - 1];
        state.Rules.Remove(rule);
        return rule;
    }

    private static IReadOnlyList<UfwRuleMutationResult> Mutate(
        UfwMockState state,
        ParsedRuleRequest request,
        bool ipv6Enabled,
        RulePlacement placement,
        int? insertNumber)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<UfwMockRule> concreteRules = request.Materialize(ipv6Enabled);
        if (placement == RulePlacement.Insert && (insertNumber is null || insertNumber <= 0))
        {
            throw new UfwCliException($"Invalid position '{insertNumber}'.");
        }

        IReadOnlyDictionary<FirewallAddressFamily, int?>? insertionPositions = placement == RulePlacement.Insert
            ? ResolveInsertPositions(
                UfwRuleVisibility.GetObservableRules(state, ipv6Enabled),
                concreteRules,
                request.Rule.Specification.AddressFamily,
                insertNumber!.Value)
            : null;

        List<UfwRuleMutationResult> mutationResults = [];
        foreach (UfwMockRule rule in concreteRules)
        {
            UfwMockRule? existing = state.Rules.FirstOrDefault(candidate => UfwRuleComparer.SemanticallyEqual(candidate, rule));
            if (existing is not null)
            {
                if (placement == RulePlacement.Append && !string.Equals(existing.Specification.Comment, rule.Specification.Comment, StringComparison.Ordinal))
                {
                    existing.Specification.Comment = rule.Specification.Comment;
                    mutationResults.Add(new UfwRuleMutationResult(rule, UfwRuleMutationKind.Updated));
                }
                else
                {
                    mutationResults.Add(new UfwRuleMutationResult(rule, UfwRuleMutationKind.Skipped));
                }
                continue;
            }

            int? familyInsertPosition = insertionPositions?.GetValueOrDefault(rule.Specification.AddressFamily);
            InsertRule(state.Rules, rule, placement, familyInsertPosition);
            mutationResults.Add(new UfwRuleMutationResult(rule, placement == RulePlacement.Insert ? UfwRuleMutationKind.Inserted : UfwRuleMutationKind.Added));
        }
        return mutationResults;
    }

    private static void InsertRule(List<UfwMockRule> rules, UfwMockRule rule, RulePlacement placement, int? familyInsertPosition)
    {
        FirewallAddressFamily family = rule.Specification.AddressFamily;
        int familyStart = family == FirewallAddressFamily.IPv6
            ? rules.FindIndex(static candidate => candidate.Specification.AddressFamily == FirewallAddressFamily.IPv6)
            : 0;
        if (familyStart < 0)
        {
            familyStart = rules.Count;
        }

        int familyCount = rules.Count(candidate => candidate.Specification.AddressFamily == family);
        int index = placement switch
        {
            RulePlacement.Append => familyStart + familyCount,
            RulePlacement.Prepend => familyStart,
            RulePlacement.Insert when familyInsertPosition.HasValue => ResolveInsertIndex(familyStart, familyCount, familyInsertPosition.Value),
            RulePlacement.Insert => familyStart + familyCount,
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, null),
        };
        rules.Insert(index, rule);
    }

    private static IReadOnlyDictionary<FirewallAddressFamily, int?> ResolveInsertPositions(
        IReadOnlyList<UfwMockRule> rules,
        IReadOnlyList<UfwMockRule> concreteRules,
        FirewallAddressFamily requestedFamily,
        int insertNumber)
    {
        int ipv4Count = rules.Count(static rule => rule.Specification.AddressFamily == FirewallAddressFamily.IPv4);
        int ipv6Count = rules.Count(static rule => rule.Specification.AddressFamily == FirewallAddressFamily.IPv6);
        int totalCount = ipv4Count + ipv6Count;
        if (insertNumber <= 0 || insertNumber > totalCount)
        {
            throw new UfwCliException($"Invalid position '{insertNumber}'.");
        }

        if (requestedFamily == FirewallAddressFamily.IPv4)
        {
            if (insertNumber > ipv4Count)
            {
                throw new UfwCliException($"Invalid position '{insertNumber}'.");
            }
            return new Dictionary<FirewallAddressFamily, int?> { [FirewallAddressFamily.IPv4] = insertNumber };
        }

        if (requestedFamily == FirewallAddressFamily.IPv6)
        {
            if (insertNumber <= ipv4Count)
            {
                throw new UfwCliException($"Invalid position '{insertNumber}'.");
            }
            return new Dictionary<FirewallAddressFamily, int?> { [FirewallAddressFamily.IPv6] = insertNumber - ipv4Count };
        }

        UfwMockRule anchor = rules[insertNumber - 1];
        FirewallAddressFamily anchorFamily = anchor.Specification.AddressFamily;
        Dictionary<FirewallAddressFamily, int?> positions = [];
        foreach (UfwMockRule concreteRule in concreteRules)
        {
            FirewallAddressFamily family = concreteRule.Specification.AddressFamily;
            if (family == anchorFamily)
            {
                positions[family] = GetFamilyPosition(rules, anchor);
                continue;
            }

            UfwMockRule? counterpart = rules.FirstOrDefault(candidate =>
                candidate.Specification.AddressFamily == family
                && UfwRuleComparer.SemanticallyEqualIgnoringAddressFamily(candidate, anchor));
            positions[family] = counterpart is null ? null : GetFamilyPosition(rules, counterpart);
        }
        return positions;
    }

    private static int GetFamilyPosition(IReadOnlyList<UfwMockRule> rules, UfwMockRule target)
    {
        FirewallAddressFamily family = target.Specification.AddressFamily;
        int position = 0;
        foreach (UfwMockRule rule in rules)
        {
            if (rule.Specification.AddressFamily != family)
            {
                continue;
            }

            position++;
            if (ReferenceEquals(rule, target))
            {
                return position;
            }
        }

        throw new InvalidOperationException("The UFW insertion anchor is not part of the current mock rule set.");
    }

    private static int ResolveInsertIndex(int familyStart, int familyCount, int familyInsertPosition)
    {
        if (familyInsertPosition <= 0 || familyInsertPosition > familyCount)
        {
            throw new UfwCliException($"Invalid position '{familyInsertPosition}'.");
        }

        return familyStart + familyInsertPosition - 1;
    }

    private enum RulePlacement
    {
        Append,
        Insert,
        Prepend,
    }
}
