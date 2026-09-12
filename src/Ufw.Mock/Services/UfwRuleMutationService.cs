using Ufw.Mock.Cli;
using Ufw.Mock.Rules;
using Ufw.Mock.State;
using Ufw.Shared.Firewall;

namespace Ufw.Mock.Services;

internal sealed class UfwRuleMutationService
{
    public IReadOnlyList<UfwRuleMutationResult> Add(UfwMockState state, ParsedRuleRequest request) =>
        Mutate(state, request, RulePlacement.Append, null);

    public IReadOnlyList<UfwRuleMutationResult> Insert(UfwMockState state, ParsedRuleRequest request, int insertNumber) =>
        Mutate(state, request, RulePlacement.Insert, insertNumber);

    public IReadOnlyList<UfwRuleMutationResult> Prepend(UfwMockState state, ParsedRuleRequest request) =>
        Mutate(state, request, RulePlacement.Prepend, null);

    public List<UfwMockRule> Delete(UfwMockState state, ParsedRuleRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<UfwMockRule> targets = request.Materialize(state.IPv6Enabled);
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

    public UfwMockRule DeleteByNumber(UfwMockState state, int displayNumber)
    {
        if (displayNumber <= 0)
        {
            throw new UfwCliException("Rule numbers are one-based.");
        }
        if (displayNumber > state.Rules.Count)
        {
            throw new UfwCliException("Could not delete non-existent rule");
        }

        UfwMockRule rule = state.Rules[displayNumber - 1];
        state.Rules.RemoveAt(displayNumber - 1);
        return rule;
    }

    private static IReadOnlyList<UfwRuleMutationResult> Mutate(
        UfwMockState state,
        ParsedRuleRequest request,
        RulePlacement placement,
        int? insertNumber)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        IReadOnlyList<UfwMockRule> concreteRules = request.Materialize(state.IPv6Enabled);
        if (placement == RulePlacement.Insert && (insertNumber is null || insertNumber <= 0))
        {
            throw new UfwCliException($"Invalid position '{insertNumber}'.");
        }

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

            InsertRule(state.Rules, rule, placement, insertNumber);
            mutationResults.Add(new UfwRuleMutationResult(rule, placement == RulePlacement.Insert ? UfwRuleMutationKind.Inserted : UfwRuleMutationKind.Added));
        }
        return mutationResults;
    }

    private static void InsertRule(List<UfwMockRule> rules, UfwMockRule rule, RulePlacement placement, int? insertNumber)
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
            RulePlacement.Insert => ResolveInsertIndex(familyStart, familyCount, insertNumber),
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, null),
        };
        rules.Insert(index, rule);
    }

    private static int ResolveInsertIndex(int familyStart, int familyCount, int? insertNumber)
    {
        if (insertNumber is null || insertNumber <= 0 || insertNumber > familyCount)
        {
            throw new UfwCliException($"Invalid position '{insertNumber}'.");
        }

        return familyStart + insertNumber.Value - 1;
    }

    private enum RulePlacement
    {
        Append,
        Insert,
        Prepend,
    }
}
