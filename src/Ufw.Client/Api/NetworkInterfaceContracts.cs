namespace Ufw.Client.Api;

public sealed class NetworkInterfaceInventoryResponse
{
    public IReadOnlyList<string> Interfaces { get; init; } = [];

    public DateTimeOffset ReconciledAt { get; init; }
}
