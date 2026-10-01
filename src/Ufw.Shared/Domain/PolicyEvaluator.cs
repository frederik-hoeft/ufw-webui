using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Evaluates one chain of a closed policy world under ordered, first-match semantics.
/// </summary>
public static class PolicyEvaluator
{
    /// <summary>Partitions <paramref name="query"/> into non-overlapping decision regions.</summary>
    public static PolicyPartition<TAddress> Evaluate<TAddress>(PolicyWorld<TAddress> world, PolicyQuery<TAddress> query)
        where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Family != world.Family)
        {
            throw new ArgumentException("The query address family does not match the policy world.", nameof(query));
        }

        PacketLayout<TAddress> layout = PacketLayout<TAddress>.Create(world, ChainProfile.For(query.Chain));
        ProductSpace querySpace = layout.Materialize(query.Constraint);
        ProductSpace undecided = querySpace;
        List<PolicyCell<TAddress>> cells = [];
        foreach (PolicyRule<TAddress> rule in world.Rules)
        {
            if (rule.Chain != query.Chain)
            {
                continue;
            }

            ProductSpace hit = undecided.Intersect(rule.Match.Product);
            if (hit.IsEmpty)
            {
                continue;
            }

            DecisionProvenance.RuleMatch provenance = new(rule.Id, rule.FamilyOrder);
            foreach (ProductRegion region in hit.Regions)
            {
                cells.Add(new PolicyCell<TAddress>(layout.ToPacket(region), rule.Decision, provenance));
            }

            undecided = undecided.Except(rule.Match.Product);
        }

        if (!undecided.IsEmpty)
        {
            DecisionProvenance.DefaultPolicy provenance = new(query.Chain);
            PolicyDecision decision = world.DefaultFor(query.Chain);
            foreach (ProductRegion region in undecided.Regions)
            {
                cells.Add(new PolicyCell<TAddress>(layout.ToPacket(region), decision, provenance));
            }
        }

        return PolicyPartition<TAddress>.Create(layout, cells, querySpace);
    }
}
