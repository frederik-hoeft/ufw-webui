using Ufw.Client.Api;
using Ufw.Client.KnownHosts;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostSuggestionsTests
{
    private static readonly KnownHostInventoryItem[] s_hosts =
    [
        Host("database", "192.0.2.10", FirewallAddressFamily.IPv4, "production postgres"),
        Host("edge-v6", "2001:db8::10", FirewallAddressFamily.IPv6, "public edge"),
        Host("office", "198.51.100.0/24", FirewallAddressFamily.IPv4, "workstations"),
    ];

    [TestMethod]
    public void Search_MatchesNameAddressAndCommentAndFiltersFamily()
    {
        CollectionAssert.AreEqual(
            new[] { "database" },
            KnownHostSuggestions.Search(s_hosts, FirewallAddressFamily.IPv4, "postgres").Select(static host => host.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "office" },
            KnownHostSuggestions.Search(s_hosts, FirewallAddressFamily.IPv4, "198.51").Select(static host => host.Name).ToArray());
        Assert.IsEmpty(KnownHostSuggestions.Search(s_hosts, FirewallAddressFamily.IPv4, "edge"));
        CollectionAssert.AreEqual(
            new[] { "edge-v6" },
            KnownHostSuggestions.Search(s_hosts, FirewallAddressFamily.Any, "edge").Select(static host => host.Name).ToArray());
    }

    [TestMethod]
    public void ResolveCompatibleAddressFamily_UsesDeclaredFamilyOrOppositeLiteral()
    {
        Assert.AreEqual(
            FirewallAddressFamily.IPv6,
            KnownHostSuggestions.ResolveCompatibleAddressFamily(FirewallAddressFamily.IPv6, "192.0.2.1"));
        Assert.AreEqual(
            FirewallAddressFamily.IPv4,
            KnownHostSuggestions.ResolveCompatibleAddressFamily(FirewallAddressFamily.Any, "192.0.2.1"));
        Assert.AreEqual(
            FirewallAddressFamily.IPv6,
            KnownHostSuggestions.ResolveCompatibleAddressFamily(FirewallAddressFamily.Any, "2001:db8::1"));
        Assert.AreEqual(
            FirewallAddressFamily.Any,
            KnownHostSuggestions.ResolveCompatibleAddressFamily(FirewallAddressFamily.Any, "not-an-address"));
    }

    [TestMethod]
    public void SelectionValue_ResolvesOnlyExplicitCompatibleSuggestion()
    {
        string selectionValue = KnownHostSuggestions.GetSelectionValue(s_hosts[0]);
        KnownHostInventoryItem? resolved = KnownHostSuggestions.ResolveSelectionValue(s_hosts, FirewallAddressFamily.IPv4, selectionValue);

        Assert.IsNotNull(resolved);
        Assert.AreEqual("192.0.2.10", resolved.Address);
        Assert.IsNull(KnownHostSuggestions.ResolveSelectionValue(s_hosts, FirewallAddressFamily.IPv6, selectionValue));
        Assert.IsNull(KnownHostSuggestions.ResolveSelectionValue(s_hosts, FirewallAddressFamily.IPv4, s_hosts[0].Name));
    }

    private static KnownHostInventoryItem Host(
        string name,
        string address,
        FirewallAddressFamily family,
        string comment) => new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Address = address,
            AddressFamily = family,
            Comment = comment,
            IsVisible = true,
        };
}
