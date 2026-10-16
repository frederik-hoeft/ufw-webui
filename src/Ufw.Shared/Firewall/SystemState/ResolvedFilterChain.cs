namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Flattened structural view of one built-in chain after chain references are inlined.
/// </summary>
public sealed class ResolvedFilterChain
{
    public required string Name { get; init; }

    public required bool ChainPresent { get; init; }

    public required FilterChainPolicy Policy { get; init; }

    public required IReadOnlyList<ResolvedFilterRule> Rules { get; init; }
}
