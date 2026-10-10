using Ufw.Shared.Firewall;
using Ufw.Web.Client.UI.Components.Rules;

namespace Ufw.Web.Client.Tests.UI.Components.Rules;

[TestClass]
public sealed class RuleActionAppearanceTests
{
    [TestMethod]
    [DataRow(FirewallAction.Allow, "rule-action rule-action-allow")]
    [DataRow(FirewallAction.Deny, "rule-action rule-action-deny")]
    [DataRow(FirewallAction.Reject, "rule-action rule-action-reject")]
    [DataRow(FirewallAction.Limit, "rule-action rule-action-limit")]
    public void CssClass_UsesTheSameSemanticColorForDesktopAndMobile(FirewallAction action, string expected)
    {
        Assert.AreEqual(expected, RuleActionAppearance.CssClass(action));
        Assert.AreEqual($"{expected} rule-action-normal", RuleActionAppearance.CssClass(action, emphasize: false));
    }

    [TestMethod]
    public void CssClass_UnknownActionHasNeutralFallback()
    {
        Assert.AreEqual("rule-action", RuleActionAppearance.CssClass((FirewallAction)999));
    }
}
