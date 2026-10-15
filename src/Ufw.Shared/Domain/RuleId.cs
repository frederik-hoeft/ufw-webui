namespace Ufw.Shared.Domain;

/// <summary>
/// Opaque semantic identity of a policy rule, independent of its position in an ordered world.
/// Equivalent rule semantics use the same identity, including across worlds; different semantics require distinct identities.
/// The domain does not interpret the text. The UFW projector uses the canonical rule identity computed from normalized firewall semantics.
/// A policy comparison rejects reuse of an identity for a different chain, decision, or resolved match.
/// </summary>
public readonly record struct RuleId : IComparable<RuleId>
{
    /// <summary>Creates a trimmed identity.</summary>
    public RuleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>Gets the identity text.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public int CompareTo(RuleId other) => string.CompareOrdinal(Value ?? string.Empty, other.Value ?? string.Empty);

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    public static bool operator <(RuleId left, RuleId right) => left.CompareTo(right) < 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    public static bool operator >(RuleId left, RuleId right) => left.CompareTo(right) > 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts before or with <paramref name="right"/>.</summary>
    public static bool operator <=(RuleId left, RuleId right) => left.CompareTo(right) <= 0;

    /// <summary>Returns <see langword="true"/> when <paramref name="left"/> sorts after or with <paramref name="right"/>.</summary>
    public static bool operator >=(RuleId left, RuleId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
