using Ufw.Shared.Firewall;

namespace Ufw.Web.Services.KnownHosts;

internal interface IKnownHostDnsResolver
{
    Task<string?> ResolveAsync(string dnsName, FirewallAddressFamily addressFamily, string? currentAddress = null, CancellationToken cancellationToken = default);
}
