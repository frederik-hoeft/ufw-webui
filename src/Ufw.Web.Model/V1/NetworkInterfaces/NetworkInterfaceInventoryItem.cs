namespace Ufw.Web.Model.V1.NetworkInterfaces;

public sealed class NetworkInterfaceInventoryItem
{
    public NetworkInterfaceInventoryItem() { }

    public NetworkInterfaceInventoryItem(Guid id, string name, string? comment, bool isVisible) =>
        (Id, Name, Comment, IsVisible) = (id, name, comment, isVisible);

    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public bool IsVisible { get; init; }
}
