namespace Ufw.Shared.Domain;

/// <summary>
/// One question put to a policy world: a concrete family, one chain, and an optional packet-space constraint.
/// </summary>
public sealed class PolicyQuery<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IMinMaxValue<TAddress>
{
    private PolicyQuery(IpFamily family, TrafficChain chain, PolicyConstraint<TAddress> constraint)
    {
        Family = family;
        Chain = chain;
        Constraint = constraint;
    }

    /// <summary>Gets the address family. It must match the world being evaluated.</summary>
    public IpFamily Family { get; }

    /// <summary>Gets the chain whose ordered rules and default policy are applied.</summary>
    public TrafficChain Chain { get; }

    /// <summary>Gets the packet space to partition. Unspecified components cover that component's whole domain.</summary>
    public PolicyConstraint<TAddress> Constraint { get; }

    /// <summary>
    /// Creates a query. A null <paramref name="constraint"/> covers the chain's entire closed world.
    /// Source and destination components, when both are present, intersect.
    /// </summary>
    public static PolicyQuery<TAddress> Create(IpFamily family, TrafficChain chain, PolicyConstraint<TAddress>? constraint = null)
    {
        if (!Enum.IsDefined(family))
        {
            throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported address family.");
        }

        if (!Enum.IsDefined(chain))
        {
            throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported traffic chain.");
        }

        return new PolicyQuery<TAddress>(family, chain, constraint ?? PolicyConstraint<TAddress>.Unconstrained);
    }
}
