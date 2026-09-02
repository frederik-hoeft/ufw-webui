using System.Collections.Immutable;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class UfwRuleArgumentBuilderTests
{
    [TestMethod]
    public void BuildAdd_UsesForceAndLongFormTokens()
    {
        ImmutableArray<string> arguments = UfwRuleArgumentBuilder.BuildAdd(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
            DestinationInterface = "eth0",
            Comment = "ssh",
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "--force", "allow", "in", "on", "eth0",
                "from", "any", "to", "any", "port", "22",
                "proto", "tcp", "comment", "ssh"
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void BuildAdd_RouteUsesInOnAndOutOn()
    {
        ImmutableArray<string> arguments = UfwRuleArgumentBuilder.BuildAdd(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.Forward,
            Protocol = FirewallProtocol.Any,
            Source = "10.0.0.0/8",
            SourceInterface = "br0",
            Destination = "192.168.0.0/16",
            DestinationInterface = "eth0",
        });

        CollectionAssert.AreEqual(
            new[]
            {
                "--force", "route", "allow",
                "in", "on", "br0",
                "out", "on", "eth0",
                "from", "10.0.0.0/8",
                "to", "192.168.0.0/16"
            },
            arguments.ToArray());
    }

    [TestMethod]
    public void BuildAdd_RejectsUnsafeComment()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => UfwRuleArgumentBuilder.BuildAdd(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Comment = "ok; rm -rf /",
        }));
    }

    [TestMethod]
    public void BuildAdd_RejectsNewlinesInInterface()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => UfwRuleArgumentBuilder.BuildAdd(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            DestinationInterface = "eth0\n--dry-run",
        }));
    }

    [TestMethod]
    public void BuildDeleteByNumber_FormatsDecimalNumber()
    {
        ImmutableArray<string> arguments = UfwRuleArgumentBuilder.BuildDeleteByNumber(12);
        CollectionAssert.AreEqual(new[] { "--force", "delete", "12" }, arguments.ToArray());
    }

    [TestMethod]
    public void BuildDeleteByNumber_RejectsNonPositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UfwRuleArgumentBuilder.BuildDeleteByNumber(0));
    }
}
