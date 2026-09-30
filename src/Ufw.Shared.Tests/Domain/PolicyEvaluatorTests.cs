using System.Numerics;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class PolicyEvaluatorTests
{
    private static readonly NetworkInterfaceName s_eth0 = new("eth0");
    private static readonly NetworkInterfaceName s_eth1 = new("eth1");

    [TestMethod]
    public void Evaluate_NoRules_CoversTheChainUniverseWithItsOwnDefault()
    {
        PolicyWorld<uint> world = World();
        PolicyPartition<uint> input = Evaluate(world, TrafficChain.Input);
        PolicyPartition<uint> output = Evaluate(world, TrafficChain.Output);
        PolicyPartition<uint> forward = Evaluate(world, TrafficChain.Forward);

        AssertSingleDefault(input, PolicyDecision.Deny, TrafficChain.Input, InputCardinality(world));
        AssertSingleDefault(output, PolicyDecision.Allow, TrafficChain.Output, InputCardinality(world));
        AssertSingleDefault(forward, PolicyDecision.Reject, TrafficChain.Forward, InputCardinality(world) * world.Interfaces.Cardinality);
        Assert.IsNotNull(input.Cells[0].Region.Ingress);
        Assert.IsNull(input.Cells[0].Region.Egress);
        Assert.IsNull(output.Cells[0].Region.Ingress);
        Assert.IsNotNull(output.Cells[0].Region.Egress);
        Assert.IsNotNull(forward.Cells[0].Region.Ingress);
        Assert.IsNotNull(forward.Cells[0].Region.Egress);
    }

    [TestMethod]
    public void Evaluate_PartialRule_LeavesTheComplementToTheDefault()
    {
        PolicyWorld<uint> world = World(Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("22")));
        PolicyPartition<uint> partition = Evaluate(world, TrafficChain.Input);

        Assert.AreEqual(InputCardinality(world), partition.Cardinality);
        AssertDisjointCover(partition);
        PolicyCell<uint> allow = Single(partition, PolicyDecision.Allow);
        Assert.IsInstanceOfType<DecisionProvenance.RuleMatch>(allow.Provenance);
        Assert.AreEqual(FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), allow.Region.Protocols);
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("22")), allow.Region.DestinationPorts);
        Assert.AreEqual(world.AddressUniverse, allow.Region.Source);
        Assert.AreEqual(world.Interfaces, allow.Region.Ingress);

        PacketPoint<uint> allowed = Point(protocol: ProtocolSymbol.Tcp, destinationPort: 22, ingress: s_eth0);
        PacketPoint<uint> otherInterface = Point(protocol: ProtocolSymbol.Tcp, destinationPort: 22, ingress: s_eth1);
        PacketPoint<uint> otherPort = Point(protocol: ProtocolSymbol.Tcp, destinationPort: 23, ingress: s_eth0);
        PacketPoint<uint> udp = Point(protocol: ProtocolSymbol.Udp, destinationPort: 22, ingress: s_eth0);
        AssertDecision(partition, allowed, PolicyDecision.Allow, "ssh");
        AssertDecision(partition, otherInterface, PolicyDecision.Allow, "ssh");
        AssertDecision(partition, otherPort, PolicyDecision.Deny, defaultPolicy: true);
        AssertDecision(partition, udp, PolicyDecision.Deny, defaultPolicy: true);
    }

    [TestMethod]
    public void Evaluate_FirstMatch_ShadowsLaterOverlapAndKeepsDistinctDecisions()
    {
        PolicyWorld<uint> world = World(
            Rule(
                "tcp22",
                TrafficChain.Input,
                PolicyDecision.Allow,
                protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp),
                destinationPorts: PacketPorts.Parse("22"),
                ingress: FiniteSet<NetworkInterfaceName>.Of(s_eth0)),
            Rule("net10", TrafficChain.Input, PolicyDecision.Deny, source: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8"))),
            Rule("dns", TrafficChain.Input, PolicyDecision.Limit, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Udp), destinationPorts: PacketPorts.Parse("53")),
            Rule("output-allow-all", TrafficChain.Output, PolicyDecision.Allow));
        PolicyPartition<uint> partition = Evaluate(world, TrafficChain.Input);

        Assert.AreEqual(InputCardinality(world), partition.Cardinality);
        AssertDisjointCover(partition);
        AssertDecision(partition, Point(source: Host("10.1.1.1"), protocol: ProtocolSymbol.Tcp, destinationPort: 22, ingress: s_eth0), PolicyDecision.Allow, "tcp22");
        AssertDecision(partition, Point(source: Host("10.1.1.1"), protocol: ProtocolSymbol.Tcp, destinationPort: 22, ingress: s_eth1), PolicyDecision.Deny, "net10");
        AssertDecision(partition, Point(source: Host("11.0.0.1"), protocol: ProtocolSymbol.Udp, destinationPort: 53, ingress: s_eth0), PolicyDecision.Limit, "dns");
        AssertDecision(partition, Point(source: Host("10.1.1.1"), protocol: ProtocolSymbol.Udp, destinationPort: 53, ingress: s_eth1), PolicyDecision.Deny, "net10");
        AssertDecision(partition, Point(source: Host("11.0.0.1"), protocol: ProtocolSymbol.Tcp, destinationPort: 80, ingress: s_eth0), PolicyDecision.Deny, defaultPolicy: true);

        PolicyPartition<uint> output = Evaluate(world, TrafficChain.Output);
        Assert.HasCount(1, output.Cells);
        Assert.AreEqual(PolicyDecision.Allow, output.Cells[0].Decision);
        Assert.IsInstanceOfType<DecisionProvenance.RuleMatch>(output.Cells[0].Provenance);
    }

    [TestMethod]
    public void Evaluate_SourceAndDestinationConstraints_Intersect()
    {
        Interval<uint> source = NetworkAddress.ParseIPv4("10.0.0.0/8");
        Interval<uint> destination = NetworkAddress.ParseIPv4("192.168.0.0/16");
        PolicyWorld<uint> world = World(Rule("web", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("80")));
        PolicyPartition<uint> partition = PolicyEvaluator.Evaluate(world, PolicyQuery<uint>.Create(IpFamily.IPv4, TrafficChain.Input, new PolicyConstraint<uint>
        {
            Source = IntervalSet<uint>.Of(source),
            Destination = IntervalSet<uint>.Of(destination),
        }));

        BigInteger expected = new BigInteger(1) << 24;
        expected *= new BigInteger(1) << 16;
        expected *= world.PortUniverse.Cardinality;
        expected *= world.PortUniverse.Cardinality;
        expected *= world.Protocols.Cardinality;
        expected *= world.Interfaces.Cardinality;
        Assert.AreEqual(expected, partition.Cardinality);
        AssertDisjointCover(partition);
        PacketSpace<uint> allowed = partition.SpaceFor(PolicyDecision.Allow);
        Assert.AreEqual(IntervalSet<uint>.Of(source), allowed.ProjectSourceAddresses());
        Assert.AreEqual(IntervalSet<uint>.Of(destination), allowed.ProjectDestinationAddresses());
        Assert.AreEqual(FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), allowed.ProjectProtocols());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("80")), allowed.ProjectDestinationPorts());
        Assert.IsNull(partition.Decide(Point(source: Host("11.0.0.1"), protocol: ProtocolSymbol.Tcp, destinationPort: 80, ingress: s_eth0)));
        Assert.IsNull(partition.Decide(Point(destination: Host("10.0.0.1"), protocol: ProtocolSymbol.Tcp, destinationPort: 80, ingress: s_eth0)));
        AssertDecision(partition, Point(source: Host("10.9.9.9"), destination: Host("192.168.1.9"), protocol: ProtocolSymbol.Tcp, destinationPort: 80, ingress: s_eth1), PolicyDecision.Allow, "web");
    }

    [TestMethod]
    public void Evaluate_Constrain_RefinesWithoutChangingDecisions()
    {
        PolicyWorld<uint> world = World(Rule("ssh", TrafficChain.Input, PolicyDecision.Reject, destinationPorts: PacketPorts.Parse("22")));
        PolicyPartition<uint> full = Evaluate(world, TrafficChain.Input);
        PolicyPartition<uint> refined = full.Constrain(new PolicyConstraint<uint>
        {
            Protocols = FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp),
            Ingress = FiniteSet<NetworkInterfaceName>.Of(s_eth0),
        });

        Assert.IsTrue(refined.Cardinality < full.Cardinality);
        Assert.AreEqual(full.Cardinality / world.Protocols.Cardinality / world.Interfaces.Cardinality, refined.Cardinality);
        PacketPoint<uint> point = Point(protocol: ProtocolSymbol.Tcp, destinationPort: 22, ingress: s_eth0);
        Assert.AreEqual(full.Decide(point)!.Decision, refined.Decide(point)!.Decision);
        Assert.AreEqual(full.Decide(point)!.Provenance, refined.Decide(point)!.Provenance);
        Assert.IsNull(refined.Decide(Point(protocol: ProtocolSymbol.Udp, destinationPort: 22, ingress: s_eth0)));
    }

    [TestMethod]
    public void Evaluate_ForwardUsesBothInterfaces_AndCustomProtocolsStayData()
    {
        ProtocolSymbol sctp = new("SCTP");
        ProtocolDefinition[] protocols = [ProtocolDefinition.Tcp, new ProtocolDefinition(sctp, usesPorts: true)];
        PolicyRuleDefinition<uint> rule = Rule(
            "route",
            TrafficChain.Forward,
            PolicyDecision.Allow,
            source: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8")),
            destination: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("192.168.0.0/16")),
            protocols: FiniteSet<ProtocolSymbol>.Of(sctp),
            destinationPorts: PacketPorts.Parse("443"),
            ingress: FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            egress: FiniteSet<NetworkInterfaceName>.Of(s_eth1));
        PolicyWorld<uint> world = PolicyWorld.CreateIPv4(protocols, FiniteSet<NetworkInterfaceName>.Of(s_eth0, s_eth1), PolicyDecision.Deny, PolicyDecision.Deny, PolicyDecision.Deny, [rule]);
        PolicyPartition<uint> partition = Evaluate(world, TrafficChain.Forward);

        Assert.AreEqual(InputCardinality(world) * world.Interfaces.Cardinality, partition.Cardinality);
        AssertDecision(
            partition,
            Point(source: Host("10.0.0.8"), destination: Host("192.168.4.4"), protocol: sctp, destinationPort: 443, ingress: s_eth0, egress: s_eth1),
            PolicyDecision.Allow,
            "route");
        AssertDecision(
            partition,
            Point(source: Host("10.0.0.8"), destination: Host("192.168.4.4"), protocol: sctp, destinationPort: 443, ingress: s_eth0, egress: s_eth0),
            PolicyDecision.Deny,
            defaultPolicy: true);
        Assert.ThrowsExactly<ArgumentException>(() => Evaluate(world, TrafficChain.Input).Decide(Point(protocol: ProtocolSymbol.Udp, ingress: s_eth0)));
    }

    [TestMethod]
    public void Evaluate_PortlessProtocol_UsesNotApplicablePortsAndPortConstraintsExcludeIt()
    {
        ProtocolSymbol icmp = new("icmp");
        ProtocolDefinition[] protocols = [ProtocolDefinition.Tcp, new ProtocolDefinition(icmp, usesPorts: false)];
        PolicyWorld<uint> world = PolicyWorld.CreateIPv4(
            protocols,
            FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            [Rule("web", TrafficChain.Input, PolicyDecision.Allow, destinationPorts: PacketPorts.Parse("80"))]);
        PolicyPartition<uint> partition = Evaluate(world, TrafficChain.Input);

        AssertDecision(partition, Point(protocol: ProtocolSymbol.Tcp, destinationPort: 80, ingress: s_eth0), PolicyDecision.Allow, "web");
        AssertDecision(partition, Point(protocol: ProtocolSymbol.Tcp, destinationPort: 81, ingress: s_eth0), PolicyDecision.Deny, defaultPolicy: true);
        AssertDecision(partition, Point(sourcePort: null, destinationPort: null, protocol: icmp, ingress: s_eth0), PolicyDecision.Deny, defaultPolicy: true);
        Assert.AreEqual(PacketPortSet.NotApplicable, partition.SpaceFor(PolicyDecision.Deny).Intersect(new PolicyConstraint<uint>
        {
            Protocols = FiniteSet<ProtocolSymbol>.Of(icmp),
        }).ProjectDestinationPorts());

        PolicyPartition<uint> impossible = PolicyEvaluator.Evaluate(world, PolicyQuery<uint>.Create(IpFamily.IPv4, TrafficChain.Input, new PolicyConstraint<uint>
        {
            Protocols = FiniteSet<ProtocolSymbol>.Of(icmp),
            DestinationPorts = PacketPorts.Parse("80"),
        }));
        Assert.IsTrue(impossible.IsEmpty);
    }

    [TestMethod]
    public void PacketSpace_FromRegion_RejectsImpossibleProtocolPortCrossProduct()
    {
        ProtocolSymbol icmp = new("icmp");
        PolicyWorld<uint> world = PolicyWorld.CreateIPv4(
            [ProtocolDefinition.Tcp, new ProtocolDefinition(icmp, usesPorts: false)],
            FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            []);
        PacketRegion<uint> invalid = new(
            world.AddressUniverse,
            PacketPortSet.Complete,
            world.AddressUniverse,
            PacketPortSet.Complete,
            FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp, icmp),
            FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            null);

        Assert.ThrowsExactly<ArgumentException>(() => PacketSpace<uint>.FromRegion(world, TrafficChain.Input, invalid));
    }

    [TestMethod]
    public void PacketSpaces_FromEquivalentWorldsCanBeComparedAcrossRuleOrderings()
    {
        PolicyRuleDefinition<uint> ssh = Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> web = Rule("web", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("80"));
        PolicyWorld<uint> original = World(ssh, web);
        PolicyWorld<uint> reordered = World(web, ssh);

        PacketSpace<uint> originalAllowed = Evaluate(original, TrafficChain.Input).SpaceFor(PolicyDecision.Allow);
        PacketSpace<uint> reorderedAllowed = Evaluate(reordered, TrafficChain.Input).SpaceFor(PolicyDecision.Allow);

        Assert.IsTrue(originalAllowed.SetEquals(reorderedAllowed));
        Assert.IsTrue(originalAllowed.Except(reorderedAllowed).IsEmpty);
        Assert.IsTrue(reorderedAllowed.Except(originalAllowed).IsEmpty);
    }

    [TestMethod]
    public void Evaluate_IPv6Rule_StaysInsideItsPrefix()
    {
        Interval<UInt128> prefix = NetworkAddress.ParseIPv6("2001:db8::/32");
        PolicyRuleDefinition<UInt128> rule = new()
        {
            Id = new RuleId("v6"),
            Chain = TrafficChain.Input,
            Decision = PolicyDecision.Allow,
            Destination = IntervalSet<UInt128>.Of(prefix),
        };
        PolicyWorld<UInt128> world = PolicyWorld.CreateIPv6(
            [ProtocolDefinition.Tcp, ProtocolDefinition.Udp],
            FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            PolicyDecision.Deny,
            PolicyDecision.Allow,
            PolicyDecision.Reject,
            [rule]);
        PolicyPartition<UInt128> partition = PolicyEvaluator.Evaluate(world, PolicyQuery<UInt128>.Create(IpFamily.IPv6, TrafficChain.Input));

        Assert.AreEqual(IntervalSet<UInt128>.Of(prefix), partition.SpaceFor(PolicyDecision.Allow).ProjectDestinationAddresses());
        Assert.AreEqual(world.AddressUniverse.Except(IntervalSet<UInt128>.Of(prefix)), partition.SpaceFor(PolicyDecision.Deny).ProjectDestinationAddresses());
        Assert.AreEqual(IntervalSet<UInt128>.Of(prefix).Cardinality * RestOfIpv6(world), partition.SpaceFor(PolicyDecision.Allow).Cardinality);
        UInt128 inside = NetworkAddress.ParseIPv6("2001:db8::1").Start;
        UInt128 outside = NetworkAddress.ParseIPv6("2001:db9::1").Start;
        Assert.AreEqual(PolicyDecision.Allow, partition.Decide(Ip6(inside))!.Decision);
        Assert.AreEqual(PolicyDecision.Deny, partition.Decide(Ip6(outside))!.Decision);
    }

    [TestMethod]
    public void Evaluate_RandomPoints_MatchAnIndependentFirstMatchLoop()
    {
        Random random = new(20260328);
        List<PolicyRuleDefinition<uint>> rules = [];
        for (int index = 0; index < 8; index++)
        {
            rules.Add(RandomRule(random, index));
        }

        PolicyWorld<uint> world = World([.. rules]);
        foreach (TrafficChain chain in new[] { TrafficChain.Input, TrafficChain.Output, TrafficChain.Forward })
        {
            PolicyPartition<uint> partition = Evaluate(world, chain);
            Assert.AreEqual(Cardinality(world, chain), partition.Cardinality);
            AssertDisjointCover(partition);
            for (int sample = 0; sample < 40; sample++)
            {
                PacketPoint<uint> point = RandomPoint(random, chain);
                (PolicyDecision decision, DecisionProvenance provenance) = Brute(world, chain, point);
                PolicyCell<uint>? cell = partition.Decide(point);
                Assert.IsNotNull(cell);
                Assert.AreEqual(decision, cell.Decision);
                Assert.AreEqual(provenance, cell.Provenance);
            }
        }
    }

    [TestMethod]
    public void Create_RejectsClosedWorldViolations()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => World(Rule("bad", TrafficChain.Input, (PolicyDecision)99)));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyWorld.CreateIPv4(
            Array.Empty<ProtocolDefinition>(),
            FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            []));
        Assert.ThrowsExactly<ArgumentException>(() => World(Rule(
            "bad-iface",
            TrafficChain.Input,
            PolicyDecision.Allow,
            ingress: FiniteSet<NetworkInterfaceName>.Of(new NetworkInterfaceName("missing")))));
        Assert.ThrowsExactly<ArgumentException>(() => World(Rule("bad-axis", TrafficChain.Output, PolicyDecision.Allow, ingress: FiniteSet<NetworkInterfaceName>.Of(s_eth0))));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PolicyWorld.CreateIPv4(
            [ProtocolDefinition.Tcp],
            FiniteSet<NetworkInterfaceName>.Of(s_eth0),
            PolicyDecision.Limit,
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            []));
    }

    private static void AssertSingleDefault(PolicyPartition<uint> partition, PolicyDecision decision, TrafficChain chain, BigInteger cardinality)
    {
        Assert.AreEqual(cardinality, partition.Cardinality);
        Assert.HasCount(1, partition.Cells);
        Assert.AreEqual(decision, partition.Cells[0].Decision);
        DecisionProvenance.DefaultPolicy provenance = (DecisionProvenance.DefaultPolicy)partition.Cells[0].Provenance;
        Assert.AreEqual(chain, provenance.Chain);
    }

    private static void AssertDisjointCover(PolicyPartition<uint> partition)
    {
        BigInteger sum = BigInteger.Zero;
        for (int index = 0; index < partition.Cells.Count; index++)
        {
            sum += partition.Cells[index].Region.Cardinality;
            for (int other = index + 1; other < partition.Cells.Count; other++)
            {
                Assert.IsFalse(partition.Cells[index].Region.Overlaps(partition.Cells[other].Region));
            }
        }

        Assert.AreEqual(partition.Cardinality, sum);
    }

    private static PolicyCell<uint> Single(PolicyPartition<uint> partition, PolicyDecision decision)
    {
        IReadOnlyList<PolicyCell<uint>> cells = partition.CellsFor(decision);
        Assert.HasCount(1, cells);
        return cells[0];
    }

    private static void AssertDecision(PolicyPartition<uint> partition, PacketPoint<uint> point, PolicyDecision decision, string? ruleId = null, bool defaultPolicy = false)
    {
        PolicyCell<uint>? cell = partition.Decide(point);
        Assert.IsNotNull(cell);
        Assert.AreEqual(decision, cell.Decision);
        if (defaultPolicy)
        {
            Assert.IsInstanceOfType<DecisionProvenance.DefaultPolicy>(cell.Provenance);
            return;
        }

        DecisionProvenance.RuleMatch match = (DecisionProvenance.RuleMatch)cell.Provenance;
        Assert.AreEqual(ruleId, match.Id.Value);
    }

    private static (PolicyDecision Decision, DecisionProvenance Provenance) Brute(PolicyWorld<uint> world, TrafficChain chain, PacketPoint<uint> point)
    {
        foreach (PolicyRule<uint> rule in world.Rules)
        {
            if (rule.Chain != chain)
            {
                continue;
            }

            bool hit = rule.Matches(point);
            if (hit)
            {
                return (rule.Decision, new DecisionProvenance.RuleMatch(rule.Id, rule.FamilyOrder));
            }
        }

        return (world.DefaultFor(chain), new DecisionProvenance.DefaultPolicy(chain));
    }

    private static PolicyPartition<uint> Evaluate(PolicyWorld<uint> world, TrafficChain chain) =>
        PolicyEvaluator.Evaluate(world, PolicyQuery<uint>.Create(IpFamily.IPv4, chain));

    private static PolicyWorld<uint> World(params PolicyRuleDefinition<uint>[] rules) =>
        PolicyWorld.CreateIPv4(
            [ProtocolDefinition.Tcp, ProtocolDefinition.Udp],
            FiniteSet<NetworkInterfaceName>.Of(s_eth0, s_eth1),
            PolicyDecision.Deny,
            PolicyDecision.Allow,
            PolicyDecision.Reject,
            rules);

    private static PolicyRuleDefinition<uint> Rule(
        string id,
        TrafficChain chain,
        PolicyDecision decision,
        IntervalSet<uint>? source = null,
        IntervalSet<ushort>? sourcePorts = null,
        IntervalSet<uint>? destination = null,
        IntervalSet<ushort>? destinationPorts = null,
        FiniteSet<ProtocolSymbol>? protocols = null,
        FiniteSet<NetworkInterfaceName>? ingress = null,
        FiniteSet<NetworkInterfaceName>? egress = null) => new()
        {
            Id = new RuleId(id),
            Chain = chain,
            Decision = decision,
            Source = source,
            SourcePorts = sourcePorts,
            Destination = destination,
            DestinationPorts = destinationPorts,
            Protocols = protocols,
            Ingress = ingress,
            Egress = egress,
        };

    private static PolicyRuleDefinition<uint> RandomRule(Random random, int index)
    {
        TrafficChain chain = (TrafficChain)random.Next(0, 3);
        PolicyDecision decision = (PolicyDecision)random.Next(0, 4);
        uint start = (uint)random.Next(0, 32) << 24;
        IntervalSet<uint>? source = random.Next(0, 2) == 0 ? null : IntervalSet<uint>.Between(start, start + 0x00ffffff);
        ushort port = (ushort)random.Next(1, 1024);
        IntervalSet<ushort>? destinationPorts = random.Next(0, 2) == 0 ? null : PacketPorts.Parse(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        FiniteSet<ProtocolSymbol>? protocols = random.Next(0, 2) == 0 ? null : FiniteSet<ProtocolSymbol>.Of(random.Next(0, 2) == 0 ? ProtocolSymbol.Tcp : ProtocolSymbol.Udp);
        FiniteSet<NetworkInterfaceName>? ingress = chain == TrafficChain.Output || random.Next(0, 2) == 0
            ? null
            : FiniteSet<NetworkInterfaceName>.Of(random.Next(0, 2) == 0 ? s_eth0 : s_eth1);
        FiniteSet<NetworkInterfaceName>? egress = chain == TrafficChain.Input || random.Next(0, 2) == 0
            ? null
            : FiniteSet<NetworkInterfaceName>.Of(random.Next(0, 2) == 0 ? s_eth0 : s_eth1);
        return Rule($"r{index}", chain, decision, source: source, destinationPorts: destinationPorts, protocols: protocols, ingress: ingress, egress: egress);
    }

    private static PacketPoint<uint> RandomPoint(Random random, TrafficChain chain)
    {
        ChainProfile profile = ChainProfile.For(chain);
        return Point(
            source: (uint)random.NextInt64(0, 1L << 32),
            sourcePort: (ushort)random.Next(1, 65536),
            destination: (uint)random.NextInt64(0, 1L << 32),
            destinationPort: (ushort)random.Next(1, 65536),
            protocol: random.Next(0, 2) == 0 ? ProtocolSymbol.Tcp : ProtocolSymbol.Udp,
            ingress: profile.HasIngress ? (random.Next(0, 2) == 0 ? s_eth0 : s_eth1) : null,
            egress: profile.HasEgress ? (random.Next(0, 2) == 0 ? s_eth0 : s_eth1) : null);
    }

    private static PacketPoint<uint> Point(
        uint source = 0x0a000001,
        ushort? sourcePort = 40000,
        uint destination = 0xc0a80001,
        ushort? destinationPort = 80,
        ProtocolSymbol? protocol = null,
        NetworkInterfaceName? ingress = null,
        NetworkInterfaceName? egress = null) => new(
            source,
            sourcePort,
            destination,
            destinationPort,
            protocol ?? ProtocolSymbol.Tcp,
            ingress,
            egress);

    private static PacketPoint<UInt128> Ip6(UInt128 address) => new(UInt128.Zero, 1, address, 443, ProtocolSymbol.Tcp, s_eth0);

    private static uint Host(string address) => NetworkAddress.ParseIPv4(address).Start;

    private static BigInteger InputCardinality(PolicyWorld<uint> world) => Cardinality(world, TrafficChain.Input);

    private static BigInteger Cardinality(PolicyWorld<uint> world, TrafficChain chain)
    {
        BigInteger total = world.AddressUniverse.Cardinality
            * world.AddressUniverse.Cardinality
            * world.PortUniverse.Cardinality
            * world.PortUniverse.Cardinality
            * world.Protocols.Cardinality;
        ChainProfile profile = ChainProfile.For(chain);
        if (profile.HasIngress)
        {
            total *= world.Interfaces.Cardinality;
        }

        if (profile.HasEgress)
        {
            total *= world.Interfaces.Cardinality;
        }

        return total;
    }

    private static BigInteger RestOfIpv6(PolicyWorld<UInt128> world) =>
        world.AddressUniverse.Cardinality
        * world.PortUniverse.Cardinality
        * world.PortUniverse.Cardinality
        * world.Protocols.Cardinality
        * world.Interfaces.Cardinality;
}
