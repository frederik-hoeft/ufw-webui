using Ufw.Shared.Firewall;

namespace Ufw.Client.Api;

public sealed class KnownHostInventoryItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public FirewallAddressFamily AddressFamily { get; init; }

    public string? Comment { get; init; }

    public bool IsVisible { get; init; }
}
