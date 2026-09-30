namespace Ufw.Shared.Domain;

/// <summary>
/// One block of a policy partition: a non-empty packet rectangle, the decision that covers it, and the rule or default responsible.
/// </summary>
public sealed class PolicyCell<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Creates a cell. The region is not required to be non-empty here; the partition builder enforces that.</summary>
    public PolicyCell(PacketRegion<TAddress> region, PolicyDecision decision, DecisionProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(provenance);
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unsupported policy decision.");
        }

        Region = region;
        Decision = decision;
        Provenance = provenance;
    }

    /// <summary>Gets the packets that share this decision and provenance.</summary>
    public PacketRegion<TAddress> Region { get; }

    /// <summary>Gets the effective decision.</summary>
    public PolicyDecision Decision { get; }

    /// <summary>Gets the rule or default policy that produced <see cref="Decision"/>.</summary>
    public DecisionProvenance Provenance { get; }
}
