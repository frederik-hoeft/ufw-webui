using Ufw.Client.Components.Rules;
using Ufw.Client.Localization;
using Ufw.Client.Tests.Support;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model.Responses;

namespace Ufw.Client.Tests;

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
    }

    [TestMethod]
    public void RuleValidationMessageLocalizer_KnownMessageUsesResourceAndUnknownMessagePassesThrough()
    {
        RuleValidationMessageLocalizer localizer = new(new PassthroughStringLocalizer<ValidationStrings>());

        string known = localizer.Localize(new ModelValidationError("Action", "Action is not supported."));
        string unknown = localizer.Localize(new ModelValidationError("Custom", "Daemon-specific validation detail."));

        Assert.AreEqual("ActionUnsupported", known);
        Assert.AreEqual("Daemon-specific validation detail.", unknown);
    }
}
