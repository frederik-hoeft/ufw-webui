namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// One rule as it appears in the chain that owns it.
/// </summary>
public sealed class RawFilterRule
{
    public required FilterRuleLocation Location { get; init; }

    public required IReadOnlyList<FilterClause> Clauses { get; init; }

    public required FilterTarget Target { get; init; }
}
