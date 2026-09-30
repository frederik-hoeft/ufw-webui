using System.Diagnostics.CodeAnalysis;

namespace Ufw.Shared.Domain;

/// <summary>
/// Identifies which ordered rule or chain default produced a decision.
/// </summary>
public abstract class DecisionProvenance : IEquatable<DecisionProvenance>
{
    private DecisionProvenance()
    {
    }

    /// <inheritdoc />
    public abstract bool Equals(DecisionProvenance? other);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as DecisionProvenance);

    /// <inheritdoc />
    public abstract override int GetHashCode();

    /// <summary>A concrete rule in the family's first-match order.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Rule match and default policy are the two cases of one provenance value.")]
    public sealed class RuleMatch : DecisionProvenance
    {
        /// <summary>Creates provenance for the rule at <paramref name="familyOrder"/>.</summary>
        public RuleMatch(RuleId id, int familyOrder)
        {
            if (string.IsNullOrEmpty(id.Value))
            {
                throw new ArgumentException("A rule identity is required.", nameof(id));
            }

            if (familyOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(familyOrder), familyOrder, "Family order cannot be negative.");
            }

            Id = id;
            FamilyOrder = familyOrder;
        }

        /// <summary>Gets the rule identity. Duplicate identities stay distinguishable by <see cref="FamilyOrder"/>.</summary>
        public RuleId Id { get; }

        /// <summary>Gets the zero-based position of the rule in its address-family list, across every chain.</summary>
        public int FamilyOrder { get; }

        /// <inheritdoc />
        public override bool Equals(DecisionProvenance? other) =>
            other is RuleMatch match && Id.Equals(match.Id) && FamilyOrder == match.FamilyOrder;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(nameof(RuleMatch), Id, FamilyOrder);

        /// <inheritdoc />
        public override string ToString() => $"{Id}@{FamilyOrder}";
    }

    /// <summary>The chain's default policy, applied to whatever no earlier rule claimed.</summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Rule match and default policy are the two cases of one provenance value.")]
    public sealed class DefaultPolicy : DecisionProvenance
    {
        /// <summary>Creates provenance for the default policy of <paramref name="chain"/>.</summary>
        public DefaultPolicy(TrafficChain chain)
        {
            if (!Enum.IsDefined(chain))
            {
                throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported traffic chain.");
            }

            Chain = chain;
        }

        /// <summary>Gets the chain whose default policy decided the region.</summary>
        public TrafficChain Chain { get; }

        /// <inheritdoc />
        public override bool Equals(DecisionProvenance? other) => other is DefaultPolicy policy && Chain == policy.Chain;

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(nameof(DefaultPolicy), Chain);

        /// <inheritdoc />
        public override string ToString() => $"default:{Chain}";
    }
}
