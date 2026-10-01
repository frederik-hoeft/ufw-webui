namespace Ufw.Shared.Domain;

/// <summary>
/// Packet path the modeled UFW user policy evaluates independently.
/// </summary>
public enum TrafficChain
{
    /// <summary>Traffic addressed to the host. UFW calls this incoming.</summary>
    Input = 0,

    /// <summary>Traffic originated by the host. UFW calls this outgoing.</summary>
    Output = 1,

    /// <summary>Traffic forwarded by the host. UFW calls this routed.</summary>
    Forward = 2,
}
