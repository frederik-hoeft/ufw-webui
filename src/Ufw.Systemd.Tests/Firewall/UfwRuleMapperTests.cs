using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.Output;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class UfwRuleMapperTests
{
    [TestMethod]
    public void TestToListedRule_MapsInputAndOutputInterfacesWithoutFallbackSemantics()
    {
        UfwStatusSnapshot? snapshot = UfwStatusParser.Parse(
            """
            Status: active
            [ 1] Anywhere on eth0 ALLOW IN 192.168.0.0/16
            [ 2] 10.0.0.0/8 ALLOW OUT Anywhere on eth1
            """);
        Assert.IsNotNull(snapshot);

        ListedFirewallRule inbound = UfwRuleMapper.ToListedRule(snapshot.Rules[0]);
        Assert.IsTrue(inbound.Parsed);
        Assert.AreEqual("eth0", inbound.Rule!.DestinationInterface);
        Assert.IsNull(inbound.Rule.SourceInterface);

        ListedFirewallRule outbound = UfwRuleMapper.ToListedRule(snapshot.Rules[1]);
        Assert.IsTrue(outbound.Parsed);
        Assert.AreEqual("eth1", outbound.Rule!.SourceInterface);
        Assert.IsNull(outbound.Rule.DestinationInterface);
    }

    [TestMethod]
    public void TestToListedRule_SemanticallyInconsistentParsedRowRemainsUnaddressable()
    {
        UfwStatusSnapshot? snapshot = UfwStatusParser.Parse("Status: active\n[ 1] 192.168.1.0/24 (v6) ALLOW IN Anywhere (v6)\n");
        Assert.IsNotNull(snapshot);
        Assert.HasCount(1, snapshot.Rules);
        Assert.IsNotNull(snapshot.Rules[0].Parsed);

        ListedFirewallRule listed = UfwRuleMapper.ToListedRule(snapshot.Rules[0]);
        Assert.IsFalse(listed.Parsed);
        Assert.IsNull(listed.RuleId);
        Assert.IsNull(listed.Rule);
    }
}
