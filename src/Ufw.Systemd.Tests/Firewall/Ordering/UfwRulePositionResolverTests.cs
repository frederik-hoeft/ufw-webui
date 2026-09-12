using Ufw.Shared.Firewall;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class UfwRulePositionResolverTests
{
    [TestMethod]
    public void GetFamilyPosition_CountsOpaqueRowsWithinTheirObservedFamily()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4),
            Opaque("[ 2] unsupported ipv4 syntax"),
            Parsed(FirewallAddressFamily.IPv6),
            Opaque("[ 4] unsupported ipv6 syntax (v6)"),
        ];

        Assert.AreEqual(2, UfwRulePositionResolver.GetFamilyPosition(rules, 1));
        Assert.AreEqual(2, UfwRulePositionResolver.GetFamilyPosition(rules, 3));
        Assert.AreEqual(2, UfwRulePositionResolver.CountFamily(rules, FirewallAddressFamily.IPv4));
        Assert.AreEqual(2, UfwRulePositionResolver.CountFamily(rules, FirewallAddressFamily.IPv6));
    }

    [TestMethod]
    public void FindNextFamilyOccurrence_CanReturnOpaqueAnchor()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4),
            Opaque("[ 2] unsupported ipv4 syntax"),
            Parsed(FirewallAddressFamily.IPv6),
        ];

        Assert.AreEqual(1, UfwRulePositionResolver.FindNextFamilyOccurrence(rules, 0, FirewallAddressFamily.IPv4));
        Assert.IsNull(UfwRulePositionResolver.FindNextFamilyOccurrence(rules, 2, FirewallAddressFamily.IPv6));
    }

    private static ListedFirewallRule Parsed(FirewallAddressFamily family) => new()
    {
        Parsed = true,
        RawLine = family == FirewallAddressFamily.IPv6 ? "rule (v6)" : "rule",
        Rule = new FirewallRuleSpecification { AddressFamily = family },
    };

    private static ListedFirewallRule Opaque(string rawLine) => new()
    {
        Parsed = false,
        RawLine = rawLine,
    };
}
