using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules;

[TestClass]
public sealed class RuleSnapshotIndexTests
{
    [TestMethod]
    public void Index_TracksOccurrenceAndOneBasedFamilyPositionsWithoutObjectIdentity()
    {
        ListedFirewallRule first = Rule("a", FirewallAddressFamily.IPv4);
        ListedFirewallRule second = Rule("b", FirewallAddressFamily.IPv6);
        ListedFirewallRule third = Rule("a", FirewallAddressFamily.IPv4);
        ListedFirewallRule fourth = Rule("c", FirewallAddressFamily.IPv6);
        RuleSnapshotIndex index = new([first, second, third, fourth]);

        Assert.AreEqual(4, index.Count);
        Assert.IsTrue(index.TryGet(2, out ListedFirewallRule? selected));
        Assert.AreSame(third, selected);
        Assert.AreEqual(2, index.GetFamilyPosition(2));
        Assert.AreEqual(2, index.GetFamilyPosition(3));
        Assert.AreEqual(2, index.GetFamilyCount(FirewallAddressFamily.IPv6));
        Assert.AreEqual(FirewallAddressFamily.IPv6, index.GetFamily(3));
        Assert.IsTrue(index.TryGet(3, "c", out _));
        Assert.IsFalse(index.TryGet(3, "a", out _));
        Assert.IsFalse(index.TryGet(-1, out _));
        Assert.IsFalse(index.TryGet(4, out _));
        Assert.AreEqual(2, index.GetIdentityMultiplicity("a"));
        Assert.IsFalse(index.HasUniqueIdentity("a"));
        Assert.IsTrue(index.HasUniqueIdentity("c"));
        Assert.IsFalse(index.HasUniqueIdentity("missing"));
    }

    [TestMethod]
    public void Index_DoesNotCountMissingRuleIdentities()
    {
        RuleSnapshotIndex index = new([Rule(null, FirewallAddressFamily.IPv4), Rule(null, FirewallAddressFamily.IPv4), Rule("x", FirewallAddressFamily.IPv4)]);

        Assert.AreEqual(0, index.GetIdentityMultiplicity(string.Empty));
        Assert.AreEqual(3, index.GetFamilyPosition(2));
        Assert.IsTrue(index.HasUniqueIdentity("x"));
    }

    private static ListedFirewallRule Rule(string? id, FirewallAddressFamily family) => new()
    {
        RuleId = id,
        Parsed = true,
        Rule = new FirewallRuleSpecification { AddressFamily = family },
    };
}
