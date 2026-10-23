namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// One rule in a root-chain traversal. <see cref="Location"/> is the physical rule.
/// <see cref="ProvenanceChain"/> is the chain that declared it.
/// </summary>
public sealed class ResolvedFilterRule
{
    public required FilterRuleLocation Location { get; init; }

    public required string ProvenanceChain { get; init; }

    /// <summary>Chain names from the built-in root through the owning chain, inclusive.</summary>
    public required IReadOnlyList<string> ChainPath { get; init; }

    /// <summary>Physical jump occurrences used to reach the owning chain, in root-to-leaf order.</summary>
    public IReadOnlyList<FilterRuleLocation> JumpPath { get; init; } = [];

    public required IReadOnlyList<FilterClause> Clauses { get; init; }

    public required FilterTarget Target { get; init; }

    public UfwUserRuleReference? UserRule { get; init; }
}
