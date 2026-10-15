namespace Ufw.Shared.Domain;

/// <summary>
/// One rectangle of a policy diff: the decisions and the full provenances from both worlds.
/// </summary>
/// <remarks>
/// <see cref="DecisionChanged"/> and <see cref="ResponsibilityChanged"/> are derived from those values.
/// Responsibility uses <see cref="PolicyResponsibility"/>, so family order can differ without a responsibility shift.
/// The carried provenance values still include each world's family order for callers that need the occurrence index.
/// </remarks>
public sealed class PolicyDiffCell<TAddress>
    where TAddress : struct, IBinaryInteger<TAddress>, IUnsignedNumber<TAddress>, IMinMaxValue<TAddress>
{
    /// <summary>Creates a diff rectangle. The partition builder rejects an empty region.</summary>
    public PolicyDiffCell(
        PacketRegion<TAddress> region,
        PolicyDecision baselineDecision,
        PolicyDecision candidateDecision,
        DecisionProvenance baselineProvenance,
        DecisionProvenance candidateProvenance)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(baselineProvenance);
        ArgumentNullException.ThrowIfNull(candidateProvenance);
        if (!Enum.IsDefined(baselineDecision))
        {
            throw new ArgumentOutOfRangeException(nameof(baselineDecision), baselineDecision, "Unsupported policy decision.");
        }

        if (!Enum.IsDefined(candidateDecision))
        {
            throw new ArgumentOutOfRangeException(nameof(candidateDecision), candidateDecision, "Unsupported policy decision.");
        }

        Region = region;
        BaselineDecision = baselineDecision;
        CandidateDecision = candidateDecision;
        BaselineProvenance = baselineProvenance;
        CandidateProvenance = candidateProvenance;
    }

    /// <summary>Gets the packets that share both decisions and both provenances.</summary>
    public PacketRegion<TAddress> Region { get; }

    /// <summary>Gets the effective decision in the baseline world.</summary>
    public PolicyDecision BaselineDecision { get; }

    /// <summary>Gets the effective decision in the candidate world.</summary>
    public PolicyDecision CandidateDecision { get; }

    /// <summary>Gets the rule or default that decided <see cref="Region"/> in the baseline world, including family order.</summary>
    public DecisionProvenance BaselineProvenance { get; }

    /// <summary>Gets the rule or default that decided <see cref="Region"/> in the candidate world, including family order.</summary>
    public DecisionProvenance CandidateProvenance { get; }

    /// <summary>Gets the baseline cross-world responsibility.</summary>
    public PolicyResponsibility BaselineResponsibility => PolicyResponsibility.From(BaselineProvenance);

    /// <summary>Gets the candidate cross-world responsibility.</summary>
    public PolicyResponsibility CandidateResponsibility => PolicyResponsibility.From(CandidateProvenance);

    /// <summary>Gets a value indicating whether the effective first-match decisions differ.</summary>
    public bool DecisionChanged => BaselineDecision != CandidateDecision;

    /// <summary>
    /// Gets a value indicating whether the semantic responsibility differs.
    /// Family order is ignored. A rule identity matches the same rule identity, and a chain default matches the same chain.
    /// </summary>
    public bool ResponsibilityChanged => BaselineResponsibility != CandidateResponsibility;

    /// <summary>Gets the quadrant that combines <see cref="DecisionChanged"/> and <see cref="ResponsibilityChanged"/>.</summary>
    public PolicyDiffKind Difference => (DecisionChanged, ResponsibilityChanged) switch
    {
        (false, false) => PolicyDiffKind.Unchanged,
        (true, false) => PolicyDiffKind.DecisionOnly,
        (false, true) => PolicyDiffKind.ResponsibilityOnly,
        _ => PolicyDiffKind.Both,
    };
}
