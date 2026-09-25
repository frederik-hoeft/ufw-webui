using System.Net;
using System.Net.Sockets;
using Ufw.Shared.Firewall;

namespace Ufw.Web.Services.KnownHosts;

internal sealed class KnownHostDnsResolver : IKnownHostDnsResolver
{
    public async Task<string?> ResolveAsync(string dnsName, FirewallAddressFamily addressFamily, string? currentAddress = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dnsName);

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(dnsName.Trim(), cancellationToken);
        }
        catch (SocketException)
        {
            return null;
        }

        return SelectAddress(addresses, addressFamily, currentAddress);
    }

    internal static string? SelectAddress(IEnumerable<IPAddress> addresses, FirewallAddressFamily addressFamily, string? currentAddress = null)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        AddressFamily socketFamily = addressFamily switch
        {
            FirewallAddressFamily.IPv4 => AddressFamily.InterNetwork,
            FirewallAddressFamily.IPv6 => AddressFamily.InterNetworkV6,
            _ => throw new ArgumentOutOfRangeException(nameof(addressFamily), addressFamily, null),
        };
        IPAddress[] candidates = [.. addresses
            .Where(address => address.AddressFamily == socketFamily)
            .Distinct()
            .OrderBy(static address => Convert.ToHexString(address.GetAddressBytes()), StringComparer.Ordinal)];
        if (candidates.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(currentAddress)
            && IPAddress.TryParse(currentAddress, out IPAddress? current)
            && current.AddressFamily == socketFamily
            && candidates.Contains(current))
        {
            return current.ToString();
        }

        return candidates[0].ToString();
    }
}
