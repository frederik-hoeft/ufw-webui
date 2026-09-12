using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class RuleReinsertabilityClassifierTests
{
    private readonly RuleReinsertabilityClassifier _classifier = new(new UfwRuleCommandRenderer());

    [TestMethod]
    public void Classify_ParsedConcreteRuleWithExplicitProtocol_IsReinsertable()
    {
        ListedFirewallRule listed = Listed(CreateRule(FirewallProtocol.Tcp, destinationPorts: "22"));

        RuleReinsertability result = _classifier.Classify(listed);

        Assert.IsTrue(result.IsReinsertable);
        Assert.IsNotNull(result.Specification);
        Assert.IsNotNull(result.RenderedRule);
        Assert.IsGreaterThan(0, result.KeepPriority);
    }

    [TestMethod]
    public void Classify_UnparsedRule_IsImmutable()
    {
        ListedFirewallRule listed = new()
        {
            Parsed = false,
            RawLine = "[ 1] unsupported",
        };

        RuleReinsertability result = _classifier.Classify(listed);

        Assert.IsFalse(result.IsReinsertable);
        Assert.IsNotNull(result.Reason);
    }

    [TestMethod]
    [DataRow(FirewallProtocol.Any)]
    [DataRow(FirewallProtocol.Tcp)]
    [DataRow(FirewallProtocol.Udp)]
    public void Classify_NoPortRule_IsReinsertable(FirewallProtocol protocol)
    {
        ListedFirewallRule listed = Listed(CreateRule(protocol, destinationPorts: null));

        RuleReinsertability result = _classifier.Classify(listed);

        Assert.IsTrue(result.IsReinsertable);
        Assert.AreEqual(protocol, result.Specification!.Protocol);
        Assert.IsNotNull(result.RenderedRule);
    }

    [TestMethod]
    public void Classify_NonConcreteAddressFamily_IsImmutable()
    {
        FirewallRuleSpecification rule = CreateRule(FirewallProtocol.Tcp, destinationPorts: "22");
        rule.AddressFamily = FirewallAddressFamily.Any;

        RuleReinsertability result = _classifier.Classify(Listed(rule));

        Assert.IsFalse(result.IsReinsertable);
    }

    private static ListedFirewallRule Listed(FirewallRuleSpecification rule) => new()
    {
        Parsed = true,
        DisplayNumber = 1,
        Rule = rule,
        RuleId = RuleIdentity.Compute(rule),
        RawLine = "test",
    };

    private static FirewallRuleSpecification CreateRule(FirewallProtocol protocol, string? destinationPorts) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = protocol,
        DestinationPorts = destinationPorts,
    };
}
