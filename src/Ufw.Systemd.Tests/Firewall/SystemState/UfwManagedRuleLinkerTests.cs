using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.SystemState;
using Ufw.Systemd.Firewall.SystemState;

namespace Ufw.Systemd.Tests.Firewall.SystemState;

[TestClass]
public sealed class UfwManagedRuleLinkerTests
{
    [TestMethod]
    public void Link_UsesObservedRuleOccurrenceRatherThanFileDerivedSpecification()
    {
        FirewallRuleSpecification configSpec = Spec(FirewallProtocol.Tcp, "22");
        FirewallRuleSpecification observedSpec = Spec(FirewallProtocol.Tcp, "22");
        string id = RuleIdentity.Compute(observedSpec);
        UfwUserRuleTranslation config = Translation(0, configSpec, [new TranslatedIptablesRule
        {
            ChainName = "ufw-user-input",
            Clauses = [],
            Target = new FilterTarget { Kind = FilterTargetKind.Jump, Name = "ACCEPT", IsChainReference = false },
        }]);
        ListedFirewallRule observed = new() { Parsed = true, RuleId = id, Rule = observedSpec, DisplayNumber = 4 };

        UfwUserRuleTranslation link = AssertExactlyOne(UfwManagedRuleLinker.Link([config], [observed], FirewallAddressFamily.IPv4));

        Assert.AreSame(observedSpec, link.Specification);
        Assert.AreEqual(id, link.RuleId);
        Assert.AreSame(config.IptablesRules, link.IptablesRules);
    }

    [TestMethod]
    public void Link_DivergentOrOpaqueStatusNeverAttributesConfiguration()
    {
        FirewallRuleSpecification ssh = Spec(FirewallProtocol.Tcp, "22");
        UfwUserRuleTranslation configured = Translation(0, ssh, []);
        ListedFirewallRule another = Listed(Spec(FirewallProtocol.Tcp, "23"));
        ListedFirewallRule opaque = new() { Parsed = false, RawLine = "unrecognized status row" };
        Assert.IsEmpty(UfwManagedRuleLinker.Link([configured], [another], FirewallAddressFamily.IPv4));
        Assert.IsEmpty(UfwManagedRuleLinker.Link([configured], [opaque], FirewallAddressFamily.IPv4));
        Assert.IsEmpty(UfwManagedRuleLinker.Link([configured], [], FirewallAddressFamily.IPv4));
    }

    [TestMethod]
    public void Link_DistinguishesIdenticalManagedOccurrencesByNumberedOrder()
    {
        FirewallRuleSpecification first = Spec(FirewallProtocol.Tcp, "22");
        FirewallRuleSpecification second = Spec(FirewallProtocol.Tcp, "22");
        IReadOnlyList<UfwUserRuleTranslation> links = UfwManagedRuleLinker.Link(
            [Translation(0, first, []), Translation(1, second, [])],
            [Listed(first), Listed(second)], FirewallAddressFamily.IPv4);

        Assert.HasCount(2, links);
        Assert.AreSame(first, links[0].Specification);
        Assert.AreSame(second, links[1].Specification);
        Assert.IsEmpty(UfwManagedRuleLinker.Link([Translation(0, first, [])], [Listed(first), Listed(second)], FirewallAddressFamily.IPv4));
    }

    [TestMethod]
    public void Link_IndependentIdentitiesRemainAttributableAcrossDifferentOrderAndMissingConfiguration()
    {
        FirewallRuleSpecification ssh = Spec(FirewallProtocol.Tcp, "22");
        FirewallRuleSpecification web = Spec(FirewallProtocol.Tcp, "80");
        FirewallRuleSpecification absent = Spec(FirewallProtocol.Tcp, "443");

        IReadOnlyList<UfwUserRuleTranslation> linked = UfwManagedRuleLinker.Link(
            [Translation(0, ssh, []), Translation(1, web, []), Translation(2, absent, [])],
            [Listed(web), Listed(ssh)], FirewallAddressFamily.IPv4);

        Assert.HasCount(2, linked);
        Assert.AreEqual(0, linked[0].Index);
        Assert.AreEqual(1, linked[1].Index);
        Assert.AreEqual(RuleIdentity.Compute(ssh), linked[0].RuleId);
        Assert.AreEqual(RuleIdentity.Compute(web), linked[1].RuleId);
    }

    private static UfwUserRuleTranslation AssertExactlyOne(IReadOnlyList<UfwUserRuleTranslation> items)
    {
        Assert.HasCount(1, items);
        return items[0];
    }

    private static ListedFirewallRule Listed(FirewallRuleSpecification specification) => new()
    {
        Rule = specification,
        RuleId = RuleIdentity.Compute(specification),
        Parsed = true,
    };

    private static UfwUserRuleTranslation Translation(int index, FirewallRuleSpecification specification, IReadOnlyList<TranslatedIptablesRule> physical) => new()
    {
        Index = index,
        Specification = specification,
        RuleId = RuleIdentity.Compute(specification),
        IptablesRules = physical,
    };

    private static FirewallRuleSpecification Spec(FirewallProtocol protocol, string destinationPort) => RuleSpecificationNormalizer.Normalize(new FirewallRuleSpecification
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = protocol,
        DestinationPorts = destinationPort,
    });
}
