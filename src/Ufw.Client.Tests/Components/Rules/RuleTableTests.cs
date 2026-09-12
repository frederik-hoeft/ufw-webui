using Ufw.Client.Components.Rules;
using Ufw.Shared.Firewall;

namespace Ufw.Client.Tests.Components.Rules;

[TestClass]
public sealed class RuleTableTests
{
    [TestMethod]
    public void CanOrderRule_DoesNotDependOnSemanticRuleIdUniqueness()
    {
        ListedFirewallRule first = Rule("duplicate");
        ListedFirewallRule second = Rule("duplicate");

        Assert.IsTrue(RuleTable.CanOrderRule(first));
        Assert.IsTrue(RuleTable.CanOrderRule(second));
    }

    [TestMethod]
    public void CanOrderRule_RejectsOpaqueRowsThatCannotBeReinserted()
    {
        ListedFirewallRule opaque = new()
        {
            DisplayNumber = 1,
            Parsed = false,
            RawLine = "opaque",
        };

        Assert.IsFalse(RuleTable.CanOrderRule(opaque));
    }

    private static ListedFirewallRule Rule(string ruleId) => new()
    {
        RuleId = ruleId,
        DisplayNumber = 1,
        Parsed = true,
        RawLine = ruleId,
        Rule = new FirewallRuleSpecification(),
    };
}
