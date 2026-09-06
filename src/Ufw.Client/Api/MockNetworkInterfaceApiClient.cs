namespace Ufw.Client.Api;

internal sealed class MockNetworkInterfaceApiClient : INetworkInterfaceApiClient
{
    private readonly Dictionary<string, string?> _comments = new(StringComparer.Ordinal)
    {
        ["docker0"] = "Container bridge",
        ["eno1"] = "Primary LAN",
    };
    private readonly string[] _interfaces = ["docker0", "eno1", "eno2", "eth0", "lo", "wlan0"];
    private readonly TimeProvider _timeProvider;
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

    public Task<NetworkInterfaceInventoryResponse> UpdateCommentAsync(
        string interfaceName,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceName);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_interfaces.Contains(interfaceName, StringComparer.Ordinal))
        {
            throw new ArgumentException("The interface is not present in the mock inventory.", nameof(interfaceName));
        }

        _comments[interfaceName] = NormalizeComment(comment);
        return Task.FromResult(CreateResponse());
    }

    private NetworkInterfaceInventoryResponse CreateResponse() => new()
    {
        Interfaces = _interfaces
            .Select(name => new NetworkInterfaceInventoryItem
            {
                Name = name,
                Comment = _comments.GetValueOrDefault(name),
            })
            .ToArray(),
        ReconciledAt = _reconciledAt,
    };

    private static string? NormalizeComment(string? comment)
        => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
}
