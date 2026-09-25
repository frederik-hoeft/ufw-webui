using Ufw.Shared.Firewall;
using Ufw.Web.Services.KnownHosts;

namespace Ufw.Web.Tests.Integration.Support;

internal sealed class IntegrationKnownHostDnsResolver : IKnownHostDnsResolver
{
    private readonly Dictionary<(string Name, FirewallAddressFamily Family), string?> _results = new();

    public Task<string?> ResolveAsync(string dnsName, FirewallAddressFamily addressFamily, string? currentAddress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _results.TryGetValue((dnsName, addressFamily), out string? result);
        return Task.FromResult(result);
    }

    public void SetResult(string dnsName, FirewallAddressFamily addressFamily, string? address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dnsName);
        _results[(dnsName, addressFamily)] = address;
    }
}
