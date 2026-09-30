namespace Ufw.Shared.Domain.Projection;

/// <summary>
/// The authoritative firewall snapshot cannot be represented as a complete closed-world policy.
/// </summary>
public sealed class FirewallProjectionException : InvalidOperationException
{
    /// <summary>Creates an exception with <paramref name="message"/>.</summary>
    public FirewallProjectionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates an exception with <paramref name="message"/> and <paramref name="innerException"/>.</summary>
    public FirewallProjectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
