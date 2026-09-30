namespace Ufw.Shared.Domain;

/// <summary>
/// One resolved rule. Match sets are already closed over the world's universes, and <see cref="FamilyOrder"/> is first-match order.
/// </summary>
public sealed class PolicyRule<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    internal PolicyRule(RuleId id, int familyOrder, TrafficChain chain, PolicyDecision decision, PacketRegion<TAddress> match)
    {
        Id = id;
        FamilyOrder = familyOrder;
        Chain = chain;
        Decision = decision;
        Match = match;
    }

    /// <summary>Gets the opaque identity supplied when the world was built.</summary>
    public RuleId Id { get; }

    /// <summary>Gets the zero-based index of this rule in the family list. Chains share this order; evaluation skips other chains.</summary>
    public int FamilyOrder { get; }

    /// <summary>Gets the chain the rule belongs to.</summary>
    public TrafficChain Chain { get; }

    /// <summary>Gets the terminal decision.</summary>
    public PolicyDecision Decision { get; }

    /// <summary>Gets the packets the rule matches before earlier rules remove any of them.</summary>
    public PacketRegion<TAddress> Match { get; }

    /// <summary>Returns <see langword="true"/> when <paramref name="point"/> is inside <see cref="Match"/>.</summary>
    public bool Matches(PacketPoint<TAddress> point) => Match.Contains(point);
}
