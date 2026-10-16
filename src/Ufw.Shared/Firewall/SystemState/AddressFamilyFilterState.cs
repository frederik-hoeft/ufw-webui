namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Raw filter table and the three resolved built-in chains for one address family.
/// </summary>
public sealed class AddressFamilyFilterState
{
    public required RawFilterTable Raw { get; init; }

    public required ResolvedFilterChain Input { get; init; }

    public required ResolvedFilterChain Output { get; init; }

    public required ResolvedFilterChain Forward { get; init; }
}
