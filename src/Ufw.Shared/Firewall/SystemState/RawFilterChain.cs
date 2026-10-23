namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// One filter chain in save order, including its policy and rules.
/// </summary>
public sealed class RawFilterChain
{
    public required string Name { get; init; }

    public required FilterChainPolicy Policy { get; init; }

    public required IReadOnlyList<RawFilterRule> Rules { get; init; }
}
