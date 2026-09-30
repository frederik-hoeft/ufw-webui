namespace Ufw.Shared.Domain;

/// <summary>
/// Questions answered from a full-chain evaluation rather than by a second policy algorithm.
/// </summary>
public static class PolicyAnalysis
{
    /// <summary>
    /// Returns same-chain rules whose match is non-empty but which decide no packet.
    /// An earlier rule has already claimed every packet they could have matched.
    /// </summary>
    public static IReadOnlyList<PolicyRule<TAddress>> ShadowedRules<TAddress>(PolicyWorld<TAddress> world, TrafficChain chain)
        where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(world);
        PolicyPartition<TAddress> partition = EvaluateChain(world, chain);
        HashSet<int> responsible = [];
        foreach (DecisionProvenance.RuleMatch match in partition.ResponsibleRules)
        {
            responsible.Add(match.FamilyOrder);
        }

        List<PolicyRule<TAddress>> shadowed = [];
        foreach (PolicyRule<TAddress> rule in world.Rules)
        {
            if (rule.Chain != chain || rule.Match.Cardinality == 0)
            {
                continue;
            }

            if (!responsible.Contains(rule.FamilyOrder))
            {
                shadowed.Add(rule);
            }
        }

        return shadowed;
    }

    /// <summary>
    /// Returns same-chain rules that match no packet in the closed world, for example because they name an empty interface set.
    /// </summary>
    public static IReadOnlyList<PolicyRule<TAddress>> IneffectiveRules<TAddress>(PolicyWorld<TAddress> world, TrafficChain chain)
        where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!Enum.IsDefined(chain))
        {
            throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported traffic chain.");
        }

        List<PolicyRule<TAddress>> ineffective = [];
        foreach (PolicyRule<TAddress> rule in world.Rules)
        {
            if (rule.Chain == chain && rule.Match.Cardinality == 0)
            {
                ineffective.Add(rule);
            }
        }

        return ineffective;
    }

    /// <summary>
    /// Returns the part of <paramref name="rule"/>'s match that an earlier rule already decided.
    /// </summary>
    public static PacketSpace<TAddress> ShadowedPortion<TAddress>(PolicyWorld<TAddress> world, PolicyRule<TAddress> rule)
        where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(rule);
        bool found = false;
        foreach (PolicyRule<TAddress> candidate in world.Rules)
        {
            if (ReferenceEquals(candidate, rule))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            throw new ArgumentException("The rule does not belong to this policy world.", nameof(rule));
        }

        PolicyPartition<TAddress> partition = EvaluateChain(world, rule.Chain);
        PacketSpace<TAddress> attributed = partition.SpaceFor(new DecisionProvenance.RuleMatch(rule.Id, rule.FamilyOrder));
        return PacketSpace<TAddress>.FromRegion(world, rule.Chain, rule.Match).Except(attributed);
    }

    private static PolicyPartition<TAddress> EvaluateChain<TAddress>(PolicyWorld<TAddress> world, TrafficChain chain)
        where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
    {
        if (!Enum.IsDefined(chain))
        {
            throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported traffic chain.");
        }

        return PolicyEvaluator.Evaluate(world, PolicyQuery<TAddress>.Create(world.Family, chain));
    }
}
