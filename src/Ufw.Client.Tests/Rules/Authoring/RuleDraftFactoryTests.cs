using Ufw.Client.Rules.Authoring;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.Rules.Authoring;

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
}
