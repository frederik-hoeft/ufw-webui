using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Authoring;

namespace Ufw.Web.Client.Tests.Features.Rules.Authoring;

[TestClass]
public sealed class RuleDraftFactoryTests
{
    [TestMethod]
    public void Create_ReturnsIndependentCanonicalDefaults()
    {
        RuleDraftFactory factory = new();

        FirewallRuleSpecification first = factory.Create();
        FirewallRuleSpecification second = factory.Create();

        Assert.AreNotSame(first, second);
        Assert.AreEqual(FirewallAction.Allow, first.Action);
        Assert.AreEqual(FirewallAddressFamily.Any, first.AddressFamily);
        Assert.AreEqual(FirewallDirection.Forward, first.Direction);
        Assert.AreEqual(FirewallProtocol.Any, first.Protocol);
    }
    [TestMethod]
    public void CreateFromExisting_ReturnsIndependentNormalizedCopy()
    {
        RuleDraftFactory factory = new();
        FirewallRuleSpecification source = new()
        {
            Action = FirewallAction.Deny,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = " 10.0.0.1/24 ",
            SourcePorts = " 443,80 ",
            SourceInterface = " eth0 ",
            Destination = " 192.0.2.10 ",
            DestinationPorts = " 22 ",
            DestinationInterface = " eth1 ",
            Comment = " managed ",
        };

        FirewallRuleSpecification draft = factory.CreateFromExisting(source);

        Assert.AreNotSame(source, draft);
        Assert.AreEqual(FirewallAction.Deny, draft.Action);
        Assert.AreEqual(FirewallAddressFamily.IPv4, draft.AddressFamily);
        Assert.AreEqual(FirewallDirection.In, draft.Direction);
        Assert.AreEqual(FirewallProtocol.Tcp, draft.Protocol);
        Assert.AreEqual("10.0.0.0/24", draft.Source);
        Assert.AreEqual("80,443", draft.SourcePorts);
        Assert.AreEqual("eth0", draft.SourceInterface);
        Assert.AreEqual("192.0.2.10", draft.Destination);
        Assert.AreEqual("22", draft.DestinationPorts);
        Assert.AreEqual("eth1", draft.DestinationInterface);
        Assert.AreEqual("managed", draft.Comment);

        draft.Comment = "changed";
        Assert.AreEqual(" managed ", source.Comment);
    }

}
