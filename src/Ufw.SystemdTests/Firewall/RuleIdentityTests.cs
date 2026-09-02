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
