namespace Ufw.Shared.Firewall.SystemState;

/// <summary>
/// Built-in chain policy. User chains use <see cref="None"/>.
/// </summary>
public enum FilterChainPolicy
{
    None,
    Accept,
    Drop,
}
