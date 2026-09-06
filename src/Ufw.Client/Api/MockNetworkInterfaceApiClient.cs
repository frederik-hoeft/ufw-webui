namespace Ufw.Client.Api;

internal sealed class MockNetworkInterfaceApiClient : INetworkInterfaceApiClient
{
    private readonly TimeProvider _timeProvider;
    private readonly string[] _interfaces = ["docker0", "eno1", "eno2", "eth0", "lo", "wlan0"];
    private DateTimeOffset _reconciledAt;

    public MockNetworkInterfaceApiClient(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _reconciledAt = timeProvider.GetUtcNow();
    }

    public bool UsesMockData => true;

    public Task<NetworkInterfaceInventoryResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateResponse());
    }

    public Task<NetworkInterfaceInventoryResponse> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _reconciledAt = _timeProvider.GetUtcNow();
        return Task.FromResult(CreateResponse());
    }

    private NetworkInterfaceInventoryResponse CreateResponse() => new()
    {
        Interfaces = _interfaces,
        ReconciledAt = _reconciledAt,
    };
}
