using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses.Domain;

namespace Ufw.Systemd.Firewall.Ordering;

internal sealed class FirewallReorderPreflightEvaluator(IRuleReorderPlanner planner, IRuleReinsertabilityClassifier reinsertabilityClassifier) : IFirewallReorderPreflightEvaluator
{
    public RuleReorderPreflightResult Evaluate(RuleListResponse baseline, IReadOnlyList<int> desiredOrder)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(desiredOrder);

        if (!TryValidateDesiredOrder(baseline.Rules, desiredOrder, out string? validationDiagnostic))
        {
            return new RuleReorderPreflightResult.Rejected(validationDiagnostic);
        }

        Dictionary<int, RuleReinsertability> classifications = [];
        HashSet<int> immutableOccurrences = [];
        Dictionary<int, int> reinsertionCosts = [];
        for (int occurrenceId = 0; occurrenceId < baseline.Rules.Count; occurrenceId++)
        {
            RuleReinsertability classification = reinsertabilityClassifier.Classify(baseline.Rules[occurrenceId]);
            classifications.Add(occurrenceId, classification);
            if (!classification.IsReinsertable)
            {
                immutableOccurrences.Add(occurrenceId);
            }
            else
            {
                reinsertionCosts.Add(occurrenceId, classification.ReinsertionCost);
            }
        }

        int[] currentOrder = Enumerable.Range(0, baseline.Rules.Count).ToArray();
        int[] desiredOrderSnapshot = [.. desiredOrder];
        if (!ImmutableOrderIsFeasible(currentOrder, desiredOrderSnapshot, immutableOccurrences))
        {
            return new RuleReorderPreflightResult.Rejected("Desired ordering would require moving an immutable occurrence.");
        }

        RuleReorderPlan plan = planner.Plan(currentOrder, desiredOrderSnapshot, immutableOccurrences, reinsertionCosts);
        Dictionary<int, FirewallRuleSpecification> moveSpecifications = [];
        foreach (RuleReorderMove move in plan.Moves)
        {
            RuleReinsertability classification = classifications[move.OccurrenceId];
            if (!classification.IsReinsertable || classification.Specification is null || classification.RenderedRule is null)
            {
                throw new InvalidOperationException("The reorder planner selected an occurrence that preflight classified as immutable.");
            }

            moveSpecifications.Add(move.OccurrenceId, classification.Specification);
        }

        return new RuleReorderPreflightResult.Accepted(new RuleReorderPreflight(desiredOrderSnapshot, plan, moveSpecifications, immutableOccurrences, reinsertionCosts));
    }

    public IReadOnlyList<RuleReorderMove> CreateSafePendingPlan(RuleListResponse baseline, RuleListResponse? currentSnapshot, RuleReorderPreflight preflight)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(preflight);

        if (currentSnapshot is null || !TryMapOccurrences(baseline, currentSnapshot, out int[]? currentOrder))
        {
            return [];
        }

        if (!ImmutableOrderIsFeasible(currentOrder, preflight.DesiredOrder, preflight.ImmutableOccurrences))
        {
            return [];
        }

        return planner.Plan(currentOrder, preflight.DesiredOrder, preflight.ImmutableOccurrences, preflight.ReinsertionCosts).Moves;
    }

    private static bool TryValidateDesiredOrder(IReadOnlyList<ListedFirewallRule> baselineRules, IReadOnlyList<int> desiredOrder, [NotNullWhen(false)] out string? diagnostic)
    {
        if (desiredOrder.Count != baselineRules.Count)
        {
            diagnostic = "Order must contain every zero-based baseline occurrence ID exactly once.";
            return false;
        }

        bool[] seen = new bool[baselineRules.Count];
        foreach (int occurrenceId in desiredOrder)
        {
            if (occurrenceId < 0 || occurrenceId >= baselineRules.Count || seen[occurrenceId])
            {
                diagnostic = "Order must contain every zero-based baseline occurrence ID exactly once.";
                return false;
            }

            seen[occurrenceId] = true;
        }

        bool ipv6Seen = false;
        foreach (int occurrenceId in desiredOrder)
        {
            FirewallAddressFamily family = ListedFirewallRuleFamily.GetObservedFamily(baselineRules[occurrenceId]);
            if (family == FirewallAddressFamily.IPv6)
            {
                ipv6Seen = true;
            }
            else if (family == FirewallAddressFamily.IPv4 && ipv6Seen)
            {
                diagnostic = "UFW requires IPv4 rule materializations to remain before IPv6 rule materializations.";
                return false;
            }
        }

        diagnostic = null;
        return true;
    }

    private static bool ImmutableOrderIsFeasible(IReadOnlyList<int> currentOrder, IReadOnlyList<int> desiredOrder, IReadOnlySet<int> immutableOccurrences)
    {
        int[] desiredPositions = new int[desiredOrder.Count];
        for (int index = 0; index < desiredOrder.Count; index++)
        {
            desiredPositions[desiredOrder[index]] = index;
        }

        int previousDesiredIndex = -1;
        foreach (int occurrenceId in currentOrder)
        {
            if (!immutableOccurrences.Contains(occurrenceId))
            {
                continue;
            }

            int desiredIndex = desiredPositions[occurrenceId];
            if (desiredIndex <= previousDesiredIndex)
            {
                return false;
            }

            previousDesiredIndex = desiredIndex;
        }

        return true;
    }

    private static bool TryMapOccurrences(RuleListResponse baseline, RuleListResponse current, [NotNullWhen(true)] out int[]? order)
    {
        if (baseline.Rules.Count != current.Rules.Count || baseline.Active != current.Active)
        {
            order = null;
            return false;
        }

        bool[] used = new bool[baseline.Rules.Count];
        int[] mapped = new int[current.Rules.Count];
        for (int currentIndex = 0; currentIndex < current.Rules.Count; currentIndex++)
        {
            int match = -1;
            for (int baselineIndex = 0; baselineIndex < baseline.Rules.Count; baselineIndex++)
            {
                if (used[baselineIndex] || !FirewallRuleStateComparer.Equals(current.Rules[currentIndex], baseline.Rules[baselineIndex]))
                {
                    continue;
                }

                if (match >= 0)
                {
                    order = null;
                    return false;
                }

                match = baselineIndex;
            }

            if (match < 0)
            {
                order = null;
                return false;
            }

            used[match] = true;
            mapped[currentIndex] = match;
        }

        order = mapped;
        return true;
    }
}
