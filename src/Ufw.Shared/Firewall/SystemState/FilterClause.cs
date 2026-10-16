namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// One ordered filter on a raw iptables rule. Known UFW predicates are structured;
/// every other match is <see cref="FilterClauseKind.Opaque"/>.
/// </summary>
public sealed class FilterClause
{
    public required FilterClauseKind Kind { get; init; }

    public required bool Negated { get; init; }

    /// <summary>
    /// Canonical value for a structured clause, or the uninterpreted text of an opaque clause.
    /// </summary>
    public required string Value { get; init; }
}
