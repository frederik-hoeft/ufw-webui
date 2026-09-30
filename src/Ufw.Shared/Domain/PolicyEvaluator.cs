using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Domain;

/// <summary>
/// Evaluates one chain of a closed policy world under ordered, first-match semantics.
/// </summary>
/// <remarks>
/// The algorithm does not inspect individual packets. It subtracts each rule's match rectangle from the still-undecided
/// query and assigns the intersection to that rule. Whatever remains takes the chain default. Limit stays a distinct
/// terminal decision. Rules on other chains are ignored, and their relative absence does not change order among the rules that remain.
/// </remarks>
public static class PolicyEvaluator
{
    /// <summary>
    /// Partitions <paramref name="query"/> into non-overlapping decision regions.
    /// A null constraint component covers that component's whole domain, and source and destination constraints intersect.
    /// </summary>
    public static PolicyPartition<TAddress> Evaluate<TAddress>(PolicyWorld<TAddress> world, PolicyQuery<TAddress> query)
        where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Family != world.Family)
        {
            throw new ArgumentException("The query address family does not match the policy world.", nameof(query));
        }

        PacketLayout<TAddress> layout = PacketLayout<TAddress>.Create(world, ChainProfile.For(query.Chain));
        ProductRegion queryRegion = layout.Materialize(query.Constraint);
        ProductSpace undecided = ProductSpace.FromRegions([queryRegion]);
        List<PolicyCell<TAddress>> cells = [];
        foreach (PolicyRule<TAddress> rule in world.Rules)
        {
            if (rule.Chain != query.Chain)
            {
                continue;
            }

            ProductRegion match = layout.FromPacket(rule.Match);
            ProductSpace hit = undecided.Intersect(match);
            if (hit.IsEmpty)
            {
                continue;
            }

            DecisionProvenance.RuleMatch provenance = new(rule.Id, rule.FamilyOrder);
            foreach (ProductRegion region in hit.Regions)
            {
                cells.Add(new PolicyCell<TAddress>(layout.ToPacket(region), rule.Decision, provenance));
            }

            undecided = undecided.Except(match);
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

        return PolicyPartition<TAddress>.Create(layout, cells, ProductSpace.FromRegions([queryRegion]));
    }
}
