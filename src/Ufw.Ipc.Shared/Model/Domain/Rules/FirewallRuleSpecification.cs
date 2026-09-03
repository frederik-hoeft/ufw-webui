namespace Ufw.Ipc.Shared.Model.Domain.Rules;

/// <summary>
/// Canonical firewall rule fields used for display, add, and delete.
/// Semantic identity is derived from these fields excluding <see cref="Comment"/>.
/// </summary>
public sealed class FirewallRuleSpecification
{
    public FirewallAction Action { get; set; }

    public FirewallAddressFamily AddressFamily { get; set; }

    public FirewallDirection Direction { get; set; }

    public FirewallProtocol Protocol { get; set; }

    public string? Source { get; set; }

    public string? SourcePorts { get; set; }

    public string? SourceInterface { get; set; }

    public string? Destination { get; set; }

    public string? DestinationPorts { get; set; }

    public string? DestinationInterface { get; set; }

    public string? Comment { get; set; }
}
