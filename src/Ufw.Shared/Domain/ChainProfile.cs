namespace Ufw.Shared.Domain;

/// <summary>
/// Which interface axes exist for a <see cref="TrafficChain"/>.
/// </summary>
/// <remarks>
/// Input has ingress only, output has egress only, and forward has both. Those are the interface
/// fields UFW's user rules can name. The profile is data for the product-space layout, not a special case
/// inside rectangle subtraction.
/// </remarks>
public readonly record struct ChainProfile
{
    private ChainProfile(TrafficChain chain, bool hasIngress, bool hasEgress)
    {
        Chain = chain;
        HasIngress = hasIngress;
        HasEgress = hasEgress;
    }

    /// <summary>Gets the chain this profile describes.</summary>
    public TrafficChain Chain { get; }

    /// <summary>Gets a value indicating whether packets on this chain have an ingress interface.</summary>
    public bool HasIngress { get; }

    /// <summary>Gets a value indicating whether packets on this chain have an egress interface.</summary>
    public bool HasEgress { get; }

    /// <summary>Returns the interface axes of <paramref name="chain"/>.</summary>
    public static ChainProfile For(TrafficChain chain) => chain switch
    {
        TrafficChain.Input => new ChainProfile(TrafficChain.Input, hasIngress: true, hasEgress: false),
        TrafficChain.Output => new ChainProfile(TrafficChain.Output, hasIngress: false, hasEgress: true),
        TrafficChain.Forward => new ChainProfile(TrafficChain.Forward, hasIngress: true, hasEgress: true),
        _ => throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported traffic chain."),
    };
}
