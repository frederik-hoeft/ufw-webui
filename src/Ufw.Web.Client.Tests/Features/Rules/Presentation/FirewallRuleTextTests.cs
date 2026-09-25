using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Presentation;
using Ufw.Web.Client.Services.Localization;
using Ufw.Web.Client.Tests.Support;

namespace Ufw.Web.Client.Tests.Features.Rules.Presentation;

[TestClass]
public sealed class FirewallRuleTextTests
{
    [TestMethod]
    public void DescribeEndpoint_NormalizesAnyPortsAndInterfaceForDisplay()
    {
        FirewallRuleText text = new(new PassthroughStringLocalizer<RulesStrings>());

        string result = text.DescribeEndpoint(null, "80:90", "eno1");

        StringAssert.Contains(result, "any");
        StringAssert.Contains(result, "80-90");
        StringAssert.Contains(result, "eno1");
    }

    [TestMethod]
    public void FormatMethods_UseCanonicalTechnicalLabelsAndLocalizedSemanticLabels()
    {
        FirewallRuleText text = new(new PassthroughStringLocalizer<RulesStrings>());

        Assert.AreEqual("Allow", text.FormatAction(FirewallAction.Allow));
        Assert.AreEqual("In", text.FormatDirection(FirewallDirection.In));
        Assert.AreEqual("IPv4", text.FormatAddressFamily(FirewallAddressFamily.IPv4));
        Assert.AreEqual("IPv6", text.FormatAddressFamily(FirewallAddressFamily.IPv6));
        Assert.AreEqual("TCP", text.FormatProtocol(FirewallProtocol.Tcp));
        Assert.AreEqual("UDP", text.FormatProtocol(FirewallProtocol.Udp));
        Assert.AreEqual("AnyProtocol", text.FormatProtocol(FirewallProtocol.Any));
        Assert.AreEqual("Allow", text.FormatDefaultPolicy(FirewallDefaultPolicy.Allow));
        Assert.AreEqual("Deny", text.FormatDefaultPolicy(FirewallDefaultPolicy.Deny));
        Assert.AreEqual("Reject", text.FormatDefaultPolicy(FirewallDefaultPolicy.Reject));
    }
}
