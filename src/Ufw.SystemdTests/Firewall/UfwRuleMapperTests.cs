using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.Output;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class UfwRuleMapperTests
{
    [TestMethod]
    public void ToListedRule_MapsInputAndOutputInterfacesWithoutFallbackSemantics()
    {
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(
            "Status: active\n"
            + "[ 1] Anywhere on eth0 ALLOW IN 192.168.0.0/16\n"
            + "[ 2] 10.0.0.0/8 ALLOW OUT Anywhere on eth1\n");

        Ufw.Ipc.Shared.Model.Domain.Rules.ListedFirewallRule inbound = UfwRuleMapper.ToListedRule(snapshot.Rules[0]);
        Assert.IsTrue(inbound.Parsed);
        Assert.AreEqual("eth0", inbound.Rule!.DestinationInterface);
        Assert.IsNull(inbound.Rule.SourceInterface);

        Ufw.Ipc.Shared.Model.Domain.Rules.ListedFirewallRule outbound = UfwRuleMapper.ToListedRule(snapshot.Rules[1]);
        Assert.IsTrue(outbound.Parsed);
        Assert.AreEqual("eth1", outbound.Rule!.SourceInterface);
        Assert.IsNull(outbound.Rule.DestinationInterface);
    }

    [TestMethod]
    public void ToListedRule_SemanticallyInconsistentParsedRowRemainsUnaddressable()
    {
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(
            "Status: active\n[ 1] 192.168.1.0/24 (v6) ALLOW IN Anywhere (v6)\n");
        Assert.AreEqual(1, snapshot.Rules.Count);
        Assert.IsNotNull(snapshot.Rules[0].Parsed);

        Ufw.Ipc.Shared.Model.Domain.Rules.ListedFirewallRule listed = UfwRuleMapper.ToListedRule(snapshot.Rules[0]);
        Assert.IsFalse(listed.Parsed);
        Assert.IsNull(listed.RuleId);
        Assert.IsNull(listed.Rule);
    }
}
