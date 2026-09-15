using Ufw.Shared.Firewall;

namespace Ufw.Shared.Tests.Firewall;

[TestClass]
public sealed class ListedFirewallRuleFamilyTests
{
    [TestMethod]
    public void GetObservedFamily_UsesParsedConcreteFamily()
    {
        ListedFirewallRule rule = new()
        {
            Parsed = true,
            RawLine = "anything",
            Rule = new FirewallRuleSpecification { AddressFamily = FirewallAddressFamily.IPv6 },
        };

        Assert.AreEqual(FirewallAddressFamily.IPv6, ListedFirewallRuleFamily.GetObservedFamily(rule));
    }

    [TestMethod]
    public void GetObservedFamily_UsesUfwV6MarkerForOpaqueRows()
    {
        ListedFirewallRule ipv4 = new() { RawLine = "unsupported syntax" };
        ListedFirewallRule ipv6 = new() { RawLine = "unsupported syntax (v6)" };

        Assert.AreEqual(FirewallAddressFamily.IPv4, ListedFirewallRuleFamily.GetObservedFamily(ipv4));
        Assert.AreEqual(FirewallAddressFamily.IPv6, ListedFirewallRuleFamily.GetObservedFamily(ipv6));
    }

    [TestMethod]
    public void GetObservedFamily_RejectsRowsWithoutObservableFamily()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ListedFirewallRuleFamily.GetObservedFamily(new ListedFirewallRule()));
    }
}
