namespace Ufw.Shared.Domain;

/// <summary>
/// Cross-world identity of the rule or chain default that decided a packet.
/// </summary>
/// <remarks>
/// <see cref="DecisionProvenance.Equals(DecisionProvenance?)"/> includes family order so duplicate rule identities
/// stay distinct inside one ordered list. Family order is only that list's index, and it is shared by every chain,
/// so an insertion renumbers every later rule. Responsibility comparison ignores the index: a rule matches by
/// its semantic <see cref="RuleId"/>, and a chain default matches by chain. Comparisons validate that reused
/// identities have consistent rule semantics. The default action is a decision, not part of this key.
/// A rule and a default are never the same responsibility.
/// </remarks>
public readonly struct PolicyResponsibility : IEquatable<PolicyResponsibility>
{
    private readonly Kind _kind;
    private readonly RuleId _ruleId;
    private readonly TrafficChain _chain;

    private PolicyResponsibility(Kind kind, RuleId ruleId, TrafficChain chain)
    {
        _kind = kind;
        _ruleId = ruleId;
        _chain = chain;
    }

    /// <summary>Gets a value indicating whether a rule identity decided the packet.</summary>
    public bool IsRule => _kind == Kind.Rule;

    /// <summary>Gets a value indicating whether a chain default decided the packet.</summary>
    public bool IsDefault => _kind == Kind.Default;

    /// <summary>Gets the rule identity, or <see langword="null"/> when a chain default decided the packet.</summary>
    public RuleId? Rule => _kind == Kind.Rule ? _ruleId : null;

    /// <summary>Gets the chain whose default decided the packet, or <see langword="null"/> when a rule decided it.</summary>
    public TrafficChain? DefaultChain => _kind == Kind.Default ? _chain : null;

    /// <summary>Projects <paramref name="provenance"/> onto its cross-world responsibility.</summary>
    /// <param name="provenance">The positional provenance from one world.</param>
    /// <returns>The responsibility key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="provenance"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="provenance"/> is not a rule match or a chain default.</exception>
    public static PolicyResponsibility From(DecisionProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        return provenance switch
        {
            DecisionProvenance.RuleMatch rule => new PolicyResponsibility(Kind.Rule, rule.Id, default),
            DecisionProvenance.DefaultPolicy policy => new PolicyResponsibility(Kind.Default, default, policy.Chain),
            _ => throw new ArgumentException("Unsupported decision provenance.", nameof(provenance)),
        };
    }

    /// <inheritdoc />
    public bool Equals(PolicyResponsibility other) => _kind == other._kind
        && (_kind switch
        {
            Kind.Rule => _ruleId.Equals(other._ruleId),
            Kind.Default => _chain == other._chain,
            _ => true,
        });

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PolicyResponsibility other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _kind switch
    {
        Kind.Rule => HashCode.Combine(_kind, _ruleId),
        Kind.Default => HashCode.Combine(_kind, _chain),
        _ => HashCode.Combine(_kind),
    };

    /// <summary>Returns <see langword="true"/> when the responsibilities are the same semantic rule or the same chain default.</summary>
    public static bool operator ==(PolicyResponsibility left, PolicyResponsibility right) => left.Equals(right);

    /// <summary>Returns <see langword="true"/> when the responsibilities differ.</summary>
    public static bool operator !=(PolicyResponsibility left, PolicyResponsibility right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _kind switch
    {
        Kind.Rule => _ruleId.ToString(),
        Kind.Default => $"default:{_chain}",
        _ => "unset",
    };

    private enum Kind : byte
    {
        Unset = 0,
        Rule = 1,
        Default = 2,
    }
}
