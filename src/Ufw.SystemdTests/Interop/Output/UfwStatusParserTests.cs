using Ufw.Systemd.Interop.Output;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Interop.Output;

[TestClass]
public sealed class UfwStatusParserTests
{
    [TestMethod]
    public void Parse_EmptyActive_HasNoRules()
    {
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(UfwStatusFixtures.EMPTY_ACTIVE);
        Assert.IsTrue(snapshot.Active);
        Assert.AreEqual(0, snapshot.Rules.Count);
    }

    [TestMethod]
    public void Parse_Inactive_HasNoRules()
    {
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(UfwStatusFixtures.INACTIVE);
        Assert.IsFalse(snapshot.Active);
        Assert.AreEqual(0, snapshot.Rules.Count);
    }

    [TestMethod]
    public void Parse_NumberedRules_ExposesDisplayNumbersAndFields()
    {
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(UfwStatusFixtures.TWO_RULES);
        Assert.IsTrue(snapshot.Active);
        Assert.AreEqual(2, snapshot.Rules.Count);
        Assert.AreEqual(1, snapshot.Rules[0].DisplayNumber);
        Assert.IsNotNull(snapshot.Rules[0].Parsed);
        Assert.AreEqual("22", snapshot.Rules[0].Parsed!.DestinationPorts);
        Assert.AreEqual("ssh", snapshot.Rules[0].Parsed!.Comment);
        Assert.AreEqual(2, snapshot.Rules[1].DisplayNumber);
        Assert.AreEqual("192.168.1.0/24", snapshot.Rules[1].Parsed!.Source);
    }

    [TestMethod]
    public void Parse_Ipv6Row_IsParsedWithConcreteFamily()
    {
        string output = UfwStatusFixtures.WithRules("[ 1] 22/tcp (v6)                 ALLOW IN    Anywhere (v6)");
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(output);
        Assert.AreEqual(1, snapshot.Rules.Count);
        Assert.AreEqual(1, snapshot.Rules[0].DisplayNumber);
        Assert.IsNotNull(snapshot.Rules[0].Parsed);
        Assert.AreEqual(Ufw.Ipc.Shared.Model.Domain.Rules.FirewallAddressFamily.IPv6, snapshot.Rules[0].Parsed!.AddressFamily);
    }

    [TestMethod]
    public void Parse_PartiallySupportedRow_IsPreservedButUnparsed()
    {
        string output = UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere unexpected");
        UfwStatusSnapshot snapshot = UfwStatusParser.Parse(output);
        Assert.AreEqual(1, snapshot.Rules.Count);
        Assert.IsNull(snapshot.Rules[0].Parsed);
        StringAssert.Contains(snapshot.Rules[0].RawLine, "unexpected");
    }
}
