using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;
using Ufw.Shared.Domain.Projection;
using Ufw.Shared.Firewall;

namespace Ufw.Shared.Tests.Domain.Projection;

[TestClass]
public sealed class FirewallPolicyProjectorTests
{
    [TestMethod]
    public void ProjectIPv4_PreservesFamilyOrderAndInterfaceAxes()
    {
        FirewallRuleSpecification inbound = Inbound();
        FirewallRuleSpecification outbound = new()
        {
            Action = FirewallAction.Deny,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.Out,
            Source = "10.1.2.3/8",
            SourceInterface = "eth1",
        };
        FirewallRuleSpecification routed = new()
        {
            Action = FirewallAction.Limit,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.Forward,
            Protocol = FirewallProtocol.Any,
            SourceInterface = "eth0",
            DestinationInterface = "eth1",
        };
        FirewallRuleSpecification ipv6 = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv6,
            Direction = FirewallDirection.In,
            Destination = "2001:db8::1",
        };
        ListedFirewallRule[] listed =
        [
            Listed(inbound),
            Listed(ipv6, "2001:db8::1 ALLOW IN Anywhere (v6)"),
            Listed(outbound),
            Listed(routed),
        ];

        PolicyWorld<uint> world = FirewallPolicyProjector.ProjectIPv4(listed, Configuration(ipv6Enabled: true), ["eth1", "eth0", "eth0"]);

        Assert.HasCount(3, world.Rules);
        Assert.AreEqual(0, world.Rules[0].FamilyOrder);
        Assert.AreEqual(2, world.Rules[2].FamilyOrder);
        Assert.AreEqual(RuleIdentity.Compute(inbound), world.Rules[0].Id.Value);
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("22")), world.Rules[0].Match.ProjectDestinationPorts());
        Assert.AreEqual(FiniteSet<NetworkInterfaceName>.Of(new NetworkInterfaceName("eth0")), world.Rules[0].Match.ProjectIngress());
        Assert.IsNull(world.Rules[0].Match.ProjectEgress());
        Assert.AreEqual(IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8")), world.Rules[1].Match.ProjectSourceAddresses());
        Assert.AreEqual(FiniteSet<NetworkInterfaceName>.Of(new NetworkInterfaceName("eth1")), world.Rules[1].Match.ProjectEgress());
        Assert.AreEqual(PolicyDecision.Limit, world.Rules[2].Decision);
        Assert.AreEqual(world.Protocols, world.Rules[2].Match.ProjectProtocols());
        Assert.AreEqual(FiniteSet<NetworkInterfaceName>.Of(new NetworkInterfaceName("eth0")), world.Rules[2].Match.ProjectIngress());
        Assert.AreEqual(FiniteSet<NetworkInterfaceName>.Of(new NetworkInterfaceName("eth1")), world.Rules[2].Match.ProjectEgress());
        Assert.AreEqual(PolicyDecision.Reject, world.RoutedDefault);

        PolicyPartition<uint> partition = PolicyEvaluator.Evaluate(world, PolicyQuery<uint>.Create(IpFamily.IPv4, TrafficChain.Input));
        PacketPoint<uint> ssh = new(0x0a000001, 40000, 0xc0a80001, 22, ProtocolSymbol.Tcp, new NetworkInterfaceName("eth0"));
        Assert.AreEqual(PolicyDecision.Allow, partition.Decide(ssh)!.Decision);
        Assert.AreEqual(world.Rules[0].Id, ((DecisionProvenance.RuleMatch)partition.Decide(ssh)!.Provenance).Id);
    }

    [TestMethod]
    public void ProjectIPv4_BareZeroAddressBecomesTheWholeUniverse()
    {
        FirewallRuleSpecification specification = new()
        {
            Action = FirewallAction.Deny,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Source = "0.0.0.0",
            Protocol = FirewallProtocol.Udp,
        };

        PolicyWorld<uint> world = FirewallPolicyProjector.ProjectIPv4([specification], FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Reject, ["eth0"]);

        Assert.AreEqual(world.AddressUniverse, world.Rules[0].Match.ProjectSourceAddresses());
        Assert.AreEqual(FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Udp), world.Rules[0].Match.ProjectProtocols());
    }

    [TestMethod]
    public void Project_FailsClosedOnOpaqueRowsDisabledIpv6AndIdentityDrift()
    {
        ListedFirewallRule opaque = new()
        {
            Parsed = false,
            RawLine = "Anywhere ALLOW IN 10.0.0.0/8",
        };
        Assert.ThrowsExactly<FirewallProjectionException>(() =>
            FirewallPolicyProjector.ProjectIPv4([opaque], Configuration(ipv6Enabled: true), ["eth0"]));

        ListedFirewallRule ipv6Opaque = new()
        {
            Parsed = false,
            RawLine = "Anywhere ALLOW IN Anywhere (v6)",
        };
        PolicyWorld<uint> ipv4 = FirewallPolicyProjector.ProjectIPv4([ipv6Opaque], Configuration(ipv6Enabled: true), ["eth0"]);
        Assert.HasCount(0, ipv4.Rules);
        Assert.ThrowsExactly<FirewallProjectionException>(() =>
            FirewallPolicyProjector.ProjectIPv6([ipv6Opaque], Configuration(ipv6Enabled: false), ["eth0"]));

        FirewallRuleSpecification inbound = Inbound();
        ListedFirewallRule drifted = Listed(inbound);
        drifted.RuleId = "sha256:not-the-rule";
        Assert.ThrowsExactly<FirewallProjectionException>(() =>
            FirewallPolicyProjector.ProjectIPv4([drifted], Configuration(ipv6Enabled: true), ["eth0"]));
    }

    [TestMethod]
    public void Project_MissingInterfaceIsIneffectiveAndFamilyNeutralRulesEnterBothWorlds()
    {
        FirewallRuleSpecification neutral = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            DestinationPorts = "443",
        };
        FirewallRuleSpecification missing = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            DestinationInterface = "eth9",
        };

        PolicyWorld<uint> ipv4 = FirewallPolicyProjector.ProjectIPv4([neutral, missing], FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny, ["eth0"]);
        PolicyWorld<UInt128> ipv6 = FirewallPolicyProjector.ProjectIPv6([neutral, missing], FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Deny, ["eth0"]);

        Assert.HasCount(2, ipv4.Rules);
        Assert.HasCount(1, ipv6.Rules);
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("443")), ipv6.Rules[0].Match.ProjectDestinationPorts());
        Assert.HasCount(1, PolicyAnalysis.IneffectiveRules(ipv4, TrafficChain.Input));
        Assert.AreEqual(ipv4.Rules[1], PolicyAnalysis.IneffectiveRules(ipv4, TrafficChain.Input)[0]);
    }

    [TestMethod]
    public void Project_DuplicateSemanticsStayOrderedAndTheLaterCopyIsShadowed()
    {
        FirewallRuleSpecification first = Inbound();
        first.Comment = "one";
        FirewallRuleSpecification second = Inbound();
        second.Comment = "two";
        PolicyWorld<uint> world = FirewallPolicyProjector.ProjectIPv4(
            [first, second],
            FirewallDefaultPolicy.Deny,
            FirewallDefaultPolicy.Allow,
            FirewallDefaultPolicy.Reject,
            ["eth0"]);

        Assert.AreEqual(world.Rules[0].Id, world.Rules[1].Id);
        Assert.AreEqual(0, world.Rules[0].FamilyOrder);
        Assert.AreEqual(1, world.Rules[1].FamilyOrder);
        IReadOnlyList<PolicyRule<uint>> shadowed = PolicyAnalysis.ShadowedRules(world, TrafficChain.Input);
        Assert.HasCount(1, shadowed);
        Assert.AreEqual(1, shadowed[0].FamilyOrder);
    }

    private static FirewallRuleSpecification Inbound() => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = "22",
        DestinationInterface = "eth0",
        Comment = "ssh",
    };

    private static ListedFirewallRule Listed(FirewallRuleSpecification specification, string? raw = null) => new()
    {
        Parsed = true,
        Rule = specification,
        RuleId = RuleIdentity.Compute(specification),
        RawLine = raw ?? "normalized",
        DisplayNumber = 1,
    };

    private static FirewallConfigurationSnapshot Configuration(bool ipv6Enabled) =>
        new(ipv6Enabled, FirewallDefaultPolicy.Deny, FirewallDefaultPolicy.Allow, FirewallDefaultPolicy.Reject);
}
