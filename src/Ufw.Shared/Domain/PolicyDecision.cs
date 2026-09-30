namespace Ufw.Shared.Domain;

/// <summary>
/// Terminal outcome of a modeled UFW user rule or default policy.
/// </summary>
/// <remarks>
/// <see cref="Limit"/> is terminal for first-match purposes and still conditional: the modeled policy does not
/// know the rate or connection state that a live limit rule would consult. It is never rewritten as allow or deny.
/// </remarks>
public enum PolicyDecision
{
    /// <summary>The packet is accepted by the modeled policy.</summary>
    Allow = 0,

    /// <summary>The packet is dropped by the modeled policy.</summary>
    Deny = 1,

    /// <summary>The packet is rejected by the modeled policy.</summary>
    Reject = 2,

    /// <summary>The packet matches a rate-limited rule. The rate outcome is outside the model.</summary>
    Limit = 3,
}
