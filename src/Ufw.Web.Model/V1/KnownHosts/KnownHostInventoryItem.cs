using Ufw.Shared.Firewall;

namespace Ufw.Web.Model.V1.KnownHosts;

public sealed class KnownHostInventoryItem
{
    public KnownHostInventoryItem() { }

    public KnownHostInventoryItem(Guid id, string name, string address, FirewallAddressFamily addressFamily, string? comment, bool isVisible) =>
        (Id, Name, Address, AddressFamily, Comment, IsVisible) = (id, name, address, addressFamily, comment, isVisible);

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public FirewallAddressFamily AddressFamily { get; init; }

    public string? Comment { get; init; }

    public bool IsVisible { get; init; }
}
