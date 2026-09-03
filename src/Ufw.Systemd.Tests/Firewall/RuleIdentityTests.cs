using Ufw.Ipc.Shared.Model.Domain.Rules;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class RuleIdentityTests
{
    [TestMethod]
    public void Compute_IgnoresCommentAndNumberFormatting()
    {
        FirewallRuleSpecification left = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "Anywhere",
            Destination = "0.0.0.0/0",
            DestinationPorts = "80,22",
            Comment = "first",
        };
        FirewallRuleSpecification right = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "any",
            Destination = "any",
            DestinationPorts = "22,80",
            Comment = "second",
        };

        Assert.AreEqual(RuleIdentity.Compute(left), RuleIdentity.Compute(right));
        Assert.IsTrue(RuleIdentity.AreEqual(left, right));
    }

    [TestMethod]
    public void Compute_NormalizesIpv4AndIpv6CidrsAndPortSets()
    {
        FirewallRuleSpecification left = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "192.168.1.123/24",
            DestinationPorts = "443,80:82,81,80",
        };
        FirewallRuleSpecification right = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "192.168.1.0/24",
            DestinationPorts = "80:82,443",
        };
        Assert.AreEqual(RuleIdentity.Compute(left), RuleIdentity.Compute(right));

        FirewallRuleSpecification ipv6Left = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv6,
            Direction = FirewallDirection.In,
            Source = "2001:0db8:0001::1234/64",
        };
        FirewallRuleSpecification ipv6Right = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv6,
            Direction = FirewallDirection.In,
            Source = "2001:db8:1::/64",
        };
        Assert.AreEqual(RuleIdentity.Compute(ipv6Left), RuleIdentity.Compute(ipv6Right));
    }

    [TestMethod]
    public void Compute_AddressFamilyDistinguishesConcreteAnywhereRows()
    {
        FirewallRuleSpecification ipv4 = CreateAllow("22");
        ipv4.AddressFamily = FirewallAddressFamily.IPv4;
        FirewallRuleSpecification ipv6 = CreateAllow("22");
        ipv6.AddressFamily = FirewallAddressFamily.IPv6;

        Assert.AreNotEqual(RuleIdentity.Compute(ipv4), RuleIdentity.Compute(ipv6));
    }

    [TestMethod]
    public void Compute_DifferentPortsAreDifferentRules()
    {
        FirewallRuleSpecification left = CreateAllow(destinationPorts: "22");
        FirewallRuleSpecification right = CreateAllow(destinationPorts: "2222");
        Assert.AreNotEqual(RuleIdentity.Compute(left), RuleIdentity.Compute(right));
    }

    [TestMethod]
    public void Compute_StartsWithSha256Prefix()
    {
        string identity = RuleIdentity.Compute(CreateAllow("22"));
        Assert.StartsWith(RuleIdentity.PREFIX, identity);
    }

    private static FirewallRuleSpecification CreateAllow(string destinationPorts) => new()
    {
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = destinationPorts,
    };
}
