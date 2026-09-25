using System.Net;
using Ufw.Shared.Firewall;
using Ufw.Web.Services.KnownHosts;

namespace Ufw.Web.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostDnsResolverTests
{
    [TestMethod]
    public void SelectAddress_FiltersByFamilyAndUsesStableAddressOrdering()
    {
        IPAddress[] addresses =
        [
            IPAddress.Parse("2001:db8::1"),
            IPAddress.Parse("192.0.2.20"),
            IPAddress.Parse("192.0.2.10"),
        ];

        string? ipv4 = KnownHostDnsResolver.SelectAddress(addresses, FirewallAddressFamily.IPv4);
        string? ipv6 = KnownHostDnsResolver.SelectAddress(addresses, FirewallAddressFamily.IPv6);

        Assert.AreEqual("192.0.2.10", ipv4);
        Assert.AreEqual("2001:db8::1", ipv6);
    }

    [TestMethod]
    public void SelectAddress_CurrentAddressStillPublished_PreservesCurrentAddress()
    {
        IPAddress[] addresses = [IPAddress.Parse("192.0.2.10"), IPAddress.Parse("192.0.2.20")];

        string? selected = KnownHostDnsResolver.SelectAddress(addresses, FirewallAddressFamily.IPv4, "192.0.2.20");

        Assert.AreEqual("192.0.2.20", selected);
    }

    [TestMethod]
    public void SelectAddress_NoAddressInRequestedFamily_ReturnsNull()
    {
        string? selected = KnownHostDnsResolver.SelectAddress([IPAddress.Parse("2001:db8::1")], FirewallAddressFamily.IPv4);

        Assert.IsNull(selected);
    }
}
