namespace Ufw.Web.Client.Api.NetworkInterfaces.Model;

public sealed class NetworkInterfaceInventoryItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Comment { get; init; }

    public bool IsVisible { get; init; }
}
