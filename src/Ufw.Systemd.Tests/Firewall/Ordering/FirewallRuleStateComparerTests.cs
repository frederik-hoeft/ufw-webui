using Ufw.Shared.Firewall;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class FirewallRuleStateComparerTests
{
    [TestMethod]
    public void Equals_NormalizesSpecificationsButIncludesCommentState()
    {
        FirewallRuleSpecification left = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "Anywhere",
            DestinationPorts = "80,22",
            Comment = " managed ",
        };
        FirewallRuleSpecification normalizedEquivalent = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "any",
            DestinationPorts = "22,80",
            Comment = "managed",
        };
        FirewallRuleSpecification differentComment = normalizedEquivalent.CopyWithAddressFamily(FirewallAddressFamily.IPv4);
        differentComment.Comment = "other";

        Assert.IsTrue(FirewallRuleStateComparer.Equals(left, normalizedEquivalent));
        Assert.IsFalse(FirewallRuleStateComparer.Equals(left, differentComment));
        Assert.IsTrue(RuleIdentity.AreEqual(left, differentComment), "Semantic rule identity intentionally excludes comments.");
    }

    [TestMethod]
    public void Equals_UnparsedRows_IgnoresDisplayNumberPrefixOnly()
    {
        ListedFirewallRule left = new() { Parsed = false, RawLine = "[ 1] custom unsupported row" };
        ListedFirewallRule renumbered = new() { Parsed = false, RawLine = "[42] custom unsupported row" };
        ListedFirewallRule different = new() { Parsed = false, RawLine = "[42] different unsupported row" };

        Assert.IsTrue(FirewallRuleStateComparer.Equals(left, renumbered));
        Assert.IsFalse(FirewallRuleStateComparer.Equals(left, different));
    }
}
