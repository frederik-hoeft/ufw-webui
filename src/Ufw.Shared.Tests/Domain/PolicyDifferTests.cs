using System.Numerics;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class PolicyDifferTests
{
    [TestMethod]
    public void Responsibility_IgnoresFamilyOrderAndKeepsPositionalEqualitySeparate()
    {
        DecisionProvenance.RuleMatch first = new(new RuleId("ssh"), 0);
        DecisionProvenance.RuleMatch moved = new(new RuleId("ssh"), 4);
        DecisionProvenance.RuleMatch otherCase = new(new RuleId("SSH"), 0);
        DecisionProvenance.RuleMatch otherRule = new(new RuleId("web"), 0);
        DecisionProvenance.DefaultPolicy input = new(TrafficChain.Input);
        DecisionProvenance.DefaultPolicy output = new(TrafficChain.Output);

        Assert.AreEqual(PolicyResponsibility.From(first), PolicyResponsibility.From(moved));
        Assert.IsTrue(first.HasSameResponsibilityAs(moved));
        Assert.AreNotEqual(first, moved);
        Assert.AreNotEqual(PolicyResponsibility.From(first), PolicyResponsibility.From(otherCase));
        Assert.AreNotEqual(PolicyResponsibility.From(first), PolicyResponsibility.From(otherRule));
        Assert.AreNotEqual(PolicyResponsibility.From(first), PolicyResponsibility.From(input));
        Assert.AreEqual(PolicyResponsibility.From(input), PolicyResponsibility.From(new DecisionProvenance.DefaultPolicy(TrafficChain.Input)));
        Assert.AreNotEqual(PolicyResponsibility.From(input), PolicyResponsibility.From(output));
        Assert.IsFalse(first.HasSameResponsibilityAs(null));
        PolicyResponsibility unset = default;
        Assert.IsFalse(unset.IsRule);
        Assert.IsFalse(unset.IsDefault);
        Assert.AreNotEqual(unset, PolicyResponsibility.From(input));
        Assert.IsNull(PolicyResponsibility.From(input).Rule);
        Assert.AreEqual(TrafficChain.Input, PolicyResponsibility.From(input).DefaultChain);
        Assert.IsNull(PolicyResponsibility.From(first).DefaultChain);
        Assert.AreEqual(first.Id, PolicyResponsibility.From(first).Rule);
        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyResponsibility.From(null!));
    }

    [TestMethod]
    public void Compare_IdenticalWorlds_IsUnchangedAndKeepsPositionalProvenance()
    {
        PolicyRuleDefinition<uint>[] rules =
        [
            PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("22")),
            PolicyDiffSupport.Rule("web", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("80")),
        ];
        PolicyWorld<uint> baseline = PolicyDiffSupport.World(rules);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World(rules);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);
        PolicyPartition<uint> partition = PolicyEvaluator.Evaluate(baseline, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.HasCount(partition.Cells.Count, diff.Cells);
        Assert.IsTrue(diff.Changed().IsEmpty);
        Assert.AreEqual(diff.Cardinality, diff.Unchanged().Cardinality);
        foreach (PolicyDiffCell<uint> cell in diff.Cells)
        {
            Assert.AreEqual(PolicyDiffKind.Unchanged, cell.Difference);
            Assert.AreEqual(cell.BaselineDecision, cell.CandidateDecision);
            Assert.AreEqual(cell.BaselineProvenance, cell.CandidateProvenance);
        }
    }

    [TestMethod]
    public void Compare_InsertionAtFront_DoesNotShiftResponsibilityOfLaterRules()
    {
        PolicyRuleDefinition<uint> ssh = PolicyDiffSupport.Rule(
            "ssh",
            TrafficChain.Input,
            PolicyDecision.Allow,
            protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp),
            destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> web = PolicyDiffSupport.Rule(
            "web",
            TrafficChain.Input,
            PolicyDecision.Allow,
            protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp),
            destinationPorts: PacketPorts.Parse("80"));
        PolicyRuleDefinition<uint> inserted = PolicyDiffSupport.Rule(
            "inserted",
            TrafficChain.Input,
            PolicyDecision.Deny,
            protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp),
            destinationPorts: PacketPorts.Parse("443"));
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([ssh, web]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([inserted, ssh, web]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.IsTrue(diff.DecisionChanges().IsEmpty);
        Assert.AreEqual(FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), diff.ResponsibilityShifts().ProjectProtocols());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("443")), diff.ResponsibilityShifts().ProjectDestinationPorts());
        AssertRuledPoint(diff, baseline, candidate, query, Port(22), PolicyDiffKind.Unchanged, "ssh", 0, "ssh", 1);
        AssertRuledPoint(diff, baseline, candidate, query, Port(80), PolicyDiffKind.Unchanged, "web", 1, "web", 2);
        AssertDefaultToRule(diff, baseline, candidate, query, Port(443), PolicyDiffKind.ResponsibilityOnly, PolicyDecision.Deny, "inserted", 0);
        AssertDefaultPoint(diff, baseline, candidate, query, Port(81), PolicyDiffKind.Unchanged, PolicyDecision.Deny, PolicyDecision.Deny);
    }

    [TestMethod]
    public void Compare_FourKinds_AreIndependentAndPartitionTheQuery()
    {
        PolicyConstraint<uint> constraint = Narrow(PacketPorts.Parse("22,80,443,81,82"));
        PolicyWorld<uint> baseline = PolicyDiffSupport.World(
        [
            PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            PolicyDiffSupport.Rule("web", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("80")),
            PolicyDiffSupport.Rule("stable", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("82")),
        ]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World(
        [
            PolicyDiffSupport.Rule("filler", TrafficChain.Output, PolicyDecision.Allow),
            PolicyDiffSupport.Rule("monitor", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            PolicyDiffSupport.Rule("web-deny", TrafficChain.Input, PolicyDecision.Deny, protocols: Tcp(), destinationPorts: PacketPorts.Parse("80")),
            PolicyDiffSupport.Rule("extra", TrafficChain.Input, PolicyDecision.Reject, protocols: Tcp(), destinationPorts: PacketPorts.Parse("443")),
            PolicyDiffSupport.Rule("stable", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("82")),
        ], incoming: PolicyDecision.Allow);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input, constraint);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(new BigInteger(5), diff.Cardinality);
        Assert.HasCount(5, diff.Cells);
        Assert.AreEqual(new BigInteger(1), diff.SpaceFor(PolicyDiffKind.Unchanged).Cardinality);
        Assert.AreEqual(new BigInteger(1), diff.SpaceFor(PolicyDiffKind.DecisionOnly).Cardinality);
        Assert.AreEqual(new BigInteger(1), diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly).Cardinality);
        Assert.AreEqual(new BigInteger(2), diff.SpaceFor(PolicyDiffKind.Both).Cardinality);
        Assert.AreEqual(new BigInteger(3), diff.DecisionChanges().Cardinality);
        Assert.AreEqual(new BigInteger(3), diff.ResponsibilityShifts().Cardinality);
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("82")), diff.Unchanged().ProjectDestinationPorts());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("81")), diff.SpaceFor(PolicyDiffKind.DecisionOnly).ProjectDestinationPorts());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("22")), diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly).ProjectDestinationPorts());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("80,443")), diff.SpaceFor(PolicyDiffKind.Both).ProjectDestinationPorts());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("80,81,443")), diff.DecisionChanges().ProjectDestinationPorts());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("22,80,443")), diff.ResponsibilityShifts().ProjectDestinationPorts());

        AssertRuledPoint(diff, baseline, candidate, query, NarrowPoint(22), PolicyDiffKind.ResponsibilityOnly, "ssh", 0, "monitor", 1);
        PolicyDiffCell<uint> webChange = Classified(diff, baseline, candidate, query, NarrowPoint(80));
        Assert.AreEqual(PolicyDiffKind.Both, webChange.Difference);
        Assert.AreEqual(PolicyDecision.Allow, webChange.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Deny, webChange.CandidateDecision);
        Assert.AreEqual("web", ((DecisionProvenance.RuleMatch)webChange.BaselineProvenance).Id.Value);
        Assert.AreEqual("web-deny", ((DecisionProvenance.RuleMatch)webChange.CandidateProvenance).Id.Value);
        AssertDefaultToRule(diff, baseline, candidate, query, NarrowPoint(443), PolicyDiffKind.Both, PolicyDecision.Reject, "extra", 3);
        AssertDefaultPoint(diff, baseline, candidate, query, NarrowPoint(81), PolicyDiffKind.DecisionOnly, PolicyDecision.Deny, PolicyDecision.Allow);
        AssertRuledPoint(diff, baseline, candidate, query, NarrowPoint(82), PolicyDiffKind.Unchanged, "stable", 2, "stable", 4);
    }

    [TestMethod]
    public void Compare_PartialSourceSplit_TracksTheOverlappingHalfOnly()
    {
        Interval<uint> whole = NetworkAddress.ParseIPv4("10.0.0.0/8");
        Interval<uint> lower = NetworkAddress.ParseIPv4("10.0.0.0/9");
        Interval<uint> upper = NetworkAddress.ParseIPv4("10.128.0.0/9");
        PolicyConstraint<uint> constraint = new()
        {
            Source = IntervalSet<uint>.Of(whole),
            SourcePorts = PacketPorts.Parse("40000"),
            Destination = IntervalSet<uint>.Singleton(PolicyDiffSupport.Host("192.168.0.1")),
            DestinationPorts = PacketPorts.Parse("80"),
            Protocols = Tcp(),
            Ingress = FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0),
        };
        PolicyWorld<uint> baseline = PolicyDiffSupport.World(
        [
            PolicyDiffSupport.Rule("allow", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("80")),
        ]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World(
        [
            PolicyDiffSupport.Rule("deny-upper", TrafficChain.Input, PolicyDecision.Deny, source: IntervalSet<uint>.Of(upper), protocols: Tcp(), destinationPorts: PacketPorts.Parse("80")),
            PolicyDiffSupport.Rule("allow", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("80")),
        ]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input, constraint);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);
        BigInteger half = IntervalSet<uint>.Of(lower).Cardinality;

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(half * 2, diff.Cardinality);
        Assert.AreEqual(half, diff.Unchanged().Cardinality);
        Assert.AreEqual(half, diff.SpaceFor(PolicyDiffKind.Both).Cardinality);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.DecisionOnly).IsEmpty);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly).IsEmpty);
        Assert.AreEqual(IntervalSet<uint>.Of(lower), diff.Unchanged().ProjectSourceAddresses());
        Assert.AreEqual(IntervalSet<uint>.Of(upper), diff.Changed().ProjectSourceAddresses());
        AssertRuledPoint(
            diff,
            baseline,
            candidate,
            query,
            NarrowPoint(80, source: PolicyDiffSupport.Host("10.1.1.1")),
            PolicyDiffKind.Unchanged,
            "allow",
            0,
            "allow",
            1);
        PolicyDiffCell<uint> upperCell = Classified(diff, baseline, candidate, query, NarrowPoint(80, source: PolicyDiffSupport.Host("10.200.1.1")));
        Assert.AreEqual(PolicyDiffKind.Both, upperCell.Difference);
        Assert.AreEqual(PolicyDecision.Allow, upperCell.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Deny, upperCell.CandidateDecision);
        Assert.AreEqual("deny-upper", ((DecisionProvenance.RuleMatch)upperCell.CandidateProvenance).Id.Value);
    }

    [TestMethod]
    public void Compare_DefaultActionChange_IsDecisionOnlyAndChainLocal()
    {
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([], incoming: PolicyDecision.Allow);
        PolicyQuery<uint> input = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyQuery<uint> output = PolicyDiffSupport.Query(TrafficChain.Output);
        PolicyDiff<uint> inputDiff = PolicyDiffer.Compare(baseline, candidate, input);
        PolicyDiff<uint> outputDiff = PolicyDiffer.Compare(baseline, candidate, output);

        AssertDiff(inputDiff, baseline, candidate, input);
        AssertDiff(outputDiff, baseline, candidate, output);
        Assert.AreEqual(inputDiff.Cardinality, inputDiff.DecisionChanges().Cardinality);
        Assert.IsTrue(inputDiff.ResponsibilityShifts().IsEmpty);
        Assert.IsTrue(outputDiff.Changed().IsEmpty);
        AssertDefaultPoint(inputDiff, baseline, candidate, input, PolicyDiffSupport.Point(), PolicyDiffKind.DecisionOnly, PolicyDecision.Deny, PolicyDecision.Allow);
        PacketPoint<uint> outputPoint = PolicyDiffSupport.Point(ingress: null, ingressSpecified: true, egress: PolicyDiffSupport.Eth1);
        AssertDefaultPoint(outputDiff, baseline, candidate, output, outputPoint, PolicyDiffKind.Unchanged, PolicyDecision.Allow, PolicyDecision.Allow);
    }

    [TestMethod]
    public void Compare_Reorder_ChangesOnlyPacketsWhoseDecidingRuleChanges()
    {
        PolicyRuleDefinition<uint> allowTcp = PolicyDiffSupport.Rule("allow-tcp", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp());
        PolicyRuleDefinition<uint> denySsh = PolicyDiffSupport.Rule(
            "deny-ssh",
            TrafficChain.Input,
            PolicyDecision.Deny,
            protocols: Tcp(),
            destinationPorts: PacketPorts.Parse("22"));
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([allowTcp, denySsh]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([denySsh, allowTcp]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("22")), diff.DecisionChanges().ProjectDestinationPorts());
        Assert.AreEqual(Tcp(), diff.DecisionChanges().ProjectProtocols());
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.DecisionOnly).IsEmpty);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly).IsEmpty);
        AssertRuledPoint(diff, baseline, candidate, query, Port(80), PolicyDiffKind.Unchanged, "allow-tcp", 0, "allow-tcp", 1);
        PolicyDiffCell<uint> ssh = Classified(diff, baseline, candidate, query, Port(22));
        Assert.AreEqual(PolicyDiffKind.Both, ssh.Difference);
        Assert.AreEqual(PolicyDecision.Allow, ssh.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Deny, ssh.CandidateDecision);
        Assert.AreEqual("allow-tcp", ((DecisionProvenance.RuleMatch)ssh.BaselineProvenance).Id.Value);
        Assert.AreEqual("deny-ssh", ((DecisionProvenance.RuleMatch)ssh.CandidateProvenance).Id.Value);
        AssertDefaultPoint(diff, baseline, candidate, query, Port(53, ProtocolSymbol.Udp), PolicyDiffKind.Unchanged, PolicyDecision.Deny, PolicyDecision.Deny);
    }

    [TestMethod]
    public void Compare_PortlessProtocol_StaysDisjointFromPortBearingSpace()
    {
        ProtocolSymbol icmp = new("icmp");
        ProtocolDefinition[] protocols = [ProtocolDefinition.Tcp, new ProtocolDefinition(icmp, usesPorts: false)];
        PolicyWorld<uint> baseline = PolicyDiffSupport.World(
            [PolicyDiffSupport.Rule("web", TrafficChain.Input, PolicyDecision.Allow, destinationPorts: PacketPorts.Parse("80"))],
            protocols: protocols,
            interfaces: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0));
        PolicyWorld<uint> candidate = PolicyDiffSupport.World(
            [
                PolicyDiffSupport.Rule("web", TrafficChain.Input, PolicyDecision.Allow, destinationPorts: PacketPorts.Parse("80")),
                PolicyDiffSupport.Rule("echo", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(icmp)),
            ],
            protocols: protocols,
            interfaces: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0));
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(FiniteSet<ProtocolSymbol>.Of(icmp), diff.DecisionChanges().ProjectProtocols());
        Assert.AreEqual(PacketPortSet.NotApplicable, diff.DecisionChanges().ProjectDestinationPorts());
        Assert.AreEqual(PacketPortSet.NotApplicable, diff.ResponsibilityShifts().ProjectSourcePorts());
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.DecisionOnly).IsEmpty);
        AssertRuledPoint(diff, baseline, candidate, query, Port(80), PolicyDiffKind.Unchanged, "web", 0, "web", 0);
        AssertDefaultPoint(diff, baseline, candidate, query, Port(81), PolicyDiffKind.Unchanged, PolicyDecision.Deny, PolicyDecision.Deny);
        PacketPoint<uint> echo = PolicyDiffSupport.Point(sourcePort: null, destinationPort: null, protocol: icmp);
        PolicyDiffCell<uint> echoCell = Classified(diff, baseline, candidate, query, echo);
        Assert.AreEqual(PolicyDiffKind.Both, echoCell.Difference);
        Assert.AreEqual(PolicyDecision.Deny, echoCell.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Allow, echoCell.CandidateDecision);
        Assert.AreEqual("echo", ((DecisionProvenance.RuleMatch)echoCell.CandidateProvenance).Id.Value);
        Assert.ThrowsExactly<ArgumentException>(() => diff.Classify(PolicyDiffSupport.Point(protocol: icmp, destinationPort: 80)));
    }

    [TestMethod]
    public void Compare_ForwardChain_PreservesBothInterfaceAxes()
    {
        PolicyRuleDefinition<uint> route = PolicyDiffSupport.Rule(
            "route",
            TrafficChain.Forward,
            PolicyDecision.Allow,
            source: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8")),
            destination: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("192.168.0.0/16")),
            protocols: Tcp(),
            destinationPorts: PacketPorts.Parse("443"),
            ingress: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0),
            egress: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth1));
        PolicyRuleDefinition<uint> rejected = PolicyDiffSupport.Rule(
            "route-reject",
            TrafficChain.Forward,
            PolicyDecision.Reject,
            source: route.Source,
            destination: route.Destination,
            protocols: route.Protocols,
            destinationPorts: route.DestinationPorts,
            ingress: route.Ingress,
            egress: route.Egress);
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([route]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([rejected]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Forward);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);
        PacketPoint<uint> matched = PolicyDiffSupport.Point(
            source: PolicyDiffSupport.Host("10.1.1.1"),
            destination: PolicyDiffSupport.Host("192.168.1.9"),
            destinationPort: 443,
            ingress: PolicyDiffSupport.Eth0,
            egress: PolicyDiffSupport.Eth1);
        PacketPoint<uint> otherEgress = PolicyDiffSupport.Point(
            source: PolicyDiffSupport.Host("10.1.1.1"),
            destination: PolicyDiffSupport.Host("192.168.1.9"),
            destinationPort: 443,
            ingress: PolicyDiffSupport.Eth0,
            egress: PolicyDiffSupport.Eth0);

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0), diff.DecisionChanges().ProjectIngress());
        Assert.AreEqual(FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth1), diff.DecisionChanges().ProjectEgress());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("443")), diff.DecisionChanges().ProjectDestinationPorts());
        Assert.IsTrue(diff.ResponsibilityShifts().SetEquals(diff.DecisionChanges()));
        PolicyDiffCell<uint> cell = Classified(diff, baseline, candidate, query, matched);
        Assert.AreEqual(PolicyDiffKind.Both, cell.Difference);
        Assert.AreEqual(PolicyDecision.Allow, cell.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Reject, cell.CandidateDecision);
        AssertDefaultPoint(diff, baseline, candidate, query, otherEgress, PolicyDiffKind.Unchanged, PolicyDecision.Reject, PolicyDecision.Reject);
    }

    [TestMethod]
    public void Compare_Limit_IsDistinctFromAllow()
    {
        PolicyRuleDefinition<uint> limited = PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Limit, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> allowed = PolicyDiffSupport.Rule("ssh-allow", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([limited]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([allowed]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);
        PolicyDiffCell<uint> cell = Classified(diff, baseline, candidate, query, Port(22));

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(PolicyDiffKind.Both, cell.Difference);
        Assert.AreEqual(PolicyDecision.Limit, cell.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Allow, cell.CandidateDecision);
        Assert.IsTrue(diff.ResponsibilityShifts().SetEquals(diff.DecisionChanges()));
    }

    [TestMethod]
    public void Compare_ConstrainedQuery_CoversOnlyTheQuery()
    {
        PolicyConstraint<uint> constraint = new()
        {
            Source = IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8")),
        };
        PolicyWorld<uint> baseline = PolicyDiffSupport.World(
            [PolicyDiffSupport.Rule("inside", TrafficChain.Input, PolicyDecision.Allow, source: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8")))]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input, constraint);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);
        PolicyPartition<uint> evaluated = PolicyEvaluator.Evaluate(baseline, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.AreEqual(evaluated.Cardinality, diff.Cardinality);
        Assert.AreEqual(diff.Cardinality, diff.Changed().Cardinality);
        Assert.IsNull(diff.Classify(PolicyDiffSupport.Point(source: PolicyDiffSupport.Host("11.0.0.1"))));
        Assert.IsNotNull(diff.Classify(PolicyDiffSupport.Point(source: PolicyDiffSupport.Host("10.9.9.9"))));
    }

    [TestMethod]
    public void Compare_EmptyQuery_IsEmptyRatherThanUnchanged()
    {
        ProtocolSymbol icmp = new("icmp");
        ProtocolDefinition[] protocols = [ProtocolDefinition.Tcp, new ProtocolDefinition(icmp, usesPorts: false)];
        PolicyWorld<uint> world = PolicyDiffSupport.World([], protocols: protocols);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input, new PolicyConstraint<uint>
        {
            Protocols = FiniteSet<ProtocolSymbol>.Of(icmp),
            DestinationPorts = PacketPorts.Parse("80"),
        });
        PolicyDiff<uint> diff = PolicyDiffer.Compare(world, world, query);

        AssertDiff(diff, world, world, query);
        Assert.IsTrue(diff.IsEmpty);
        Assert.AreEqual(BigInteger.Zero, diff.Cardinality);
        Assert.IsTrue(diff.Changed().IsEmpty);
        Assert.IsTrue(diff.Unchanged().IsEmpty);
        Assert.IsNull(diff.Classify(PolicyDiffSupport.Point(sourcePort: null, destinationPort: null, protocol: icmp)));
        Assert.IsNull(diff.Classify(PolicyDiffSupport.Point()));
    }

    [TestMethod]
    public void Compare_InterfaceUniverseOrder_DoesNotAffectEquivalence()
    {
        PolicyRuleDefinition<uint> rule = PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyWorld<uint> forward = PolicyDiffSupport.World([rule], interfaces: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0, PolicyDiffSupport.Eth1));
        PolicyWorld<uint> reversed = PolicyDiffSupport.World([rule], interfaces: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth1, PolicyDiffSupport.Eth0));
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(forward, reversed, query);

        AssertDiff(diff, forward, reversed, query);
        Assert.IsTrue(diff.Changed().IsEmpty);
    }

    [TestMethod]
    public void Compare_RandomWorlds_AgreeWithFirstMatchAndTheEvaluator()
    {
        Random random = new(20260328);
        for (int round = 0; round < 4; round++)
        {
            PolicyRuleDefinition<uint>[] baselineRules = RandomRules(random, 6, $"baseline-{round}");
            PolicyRuleDefinition<uint>[] candidateRules = RandomRules(random, 6, $"candidate-{round}");
            PolicyWorld<uint> baseline = PolicyDiffSupport.World(baselineRules);
            PolicyWorld<uint> candidate = PolicyDiffSupport.World(candidateRules);
            foreach (TrafficChain chain in new[] { TrafficChain.Input, TrafficChain.Output, TrafficChain.Forward })
            {
                PolicyQuery<uint> query = PolicyDiffSupport.Query(chain);
                PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);
                PolicyPartition<uint> baselinePartition = PolicyEvaluator.Evaluate(baseline, query);
                PolicyPartition<uint> candidatePartition = PolicyEvaluator.Evaluate(candidate, query);
                Assert.AreEqual(baselinePartition.Cardinality, diff.Cardinality);
                Assert.AreEqual(candidatePartition.Cardinality, diff.Cardinality);
                PolicyDiffSupport.AssertDiffInvariants(diff);
                for (int sample = 0; sample < 20; sample++)
                {
                    PolicyDiffSupport.AssertAgrees(
                        diff,
                        baselinePartition,
                        candidatePartition,
                        baseline,
                        candidate,
                        RandomPoint(random, chain));
                }
            }
        }
    }

    [TestMethod]
    public void Compare_RejectsConflictingSemanticIdentitiesEvenWhenShadowedOrOutsideTheQuery()
    {
        PolicyRuleDefinition<uint> ssh = PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> differentAction = PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Deny, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> differentMatch = PolicyDiffSupport.Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("23"));
        PolicyRuleDefinition<uint> differentChain = PolicyDiffSupport.Rule("ssh", TrafficChain.Output, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([ssh]);
        PolicyQuery<uint> unrelated = PolicyDiffSupport.Query(TrafficChain.Input, Narrow(PacketPorts.Parse("80")));
        PolicyQuery<uint> empty = PolicyDiffSupport.Query(TrafficChain.Input, new PolicyConstraint<uint> { Protocols = FiniteSet<ProtocolSymbol>.Empty });

        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(baseline, PolicyDiffSupport.World([differentAction]), unrelated));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(baseline, PolicyDiffSupport.World([differentMatch]), unrelated));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(baseline, PolicyDiffSupport.World([differentChain]), empty));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(PolicyDiffSupport.World([ssh, differentAction]), baseline, unrelated));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(baseline, PolicyDiffSupport.World([ssh, differentMatch]), empty));
    }

    [TestMethod]
    public void Compare_AcceptsRepeatedIdenticalRuleIdentities()
    {
        PolicyRuleDefinition<uint> rule = PolicyDiffSupport.Rule("dup", TrafficChain.Input, PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyWorld<uint> baseline = PolicyDiffSupport.World([rule, rule]);
        PolicyWorld<uint> candidate = PolicyDiffSupport.World([rule]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(baseline, candidate, query);

        AssertDiff(diff, baseline, candidate, query);
        Assert.IsTrue(diff.Changed().IsEmpty);
        PolicyDiffCell<uint> cell = Classified(diff, baseline, candidate, query, Port(22));
        Assert.AreEqual(PolicyDiffKind.Unchanged, cell.Difference);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)cell.BaselineProvenance).FamilyOrder);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)cell.CandidateProvenance).FamilyOrder);
    }

    [TestMethod]
    public void Compare_RejectsClosedWorldMismatchesAndNullArguments()
    {
        PolicyWorld<uint> world = PolicyDiffSupport.World([]);
        PolicyWorld<uint> otherInterfaces = PolicyDiffSupport.World([], interfaces: FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0));
        ProtocolSymbol icmp = new("icmp");
        PolicyWorld<uint> portless = PolicyDiffSupport.World([], protocols: [ProtocolDefinition.Tcp, new ProtocolDefinition(icmp, usesPorts: false)]);
        PolicyWorld<uint> portBearing = PolicyDiffSupport.World([], protocols: [ProtocolDefinition.Tcp, new ProtocolDefinition(icmp, usesPorts: true)]);
        PolicyWorld<uint> udpMissing = PolicyDiffSupport.World([], protocols: [ProtocolDefinition.Tcp]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyDiffer.Compare(world, world, query);

        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyDiffer.Compare<uint>(null!, world, query));
        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyDiffer.Compare(world, null!, query));
        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyDiffer.Compare(world, world, null!));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(world, otherInterfaces, query));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(portless, portBearing, query));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(world, udpMissing, query));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(world, world, PolicyQuery<uint>.Create(IpFamily.IPv6, TrafficChain.Input)));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyDiffer.Compare(world, world, PolicyDiffSupport.Query(TrafficChain.Output, new PolicyConstraint<uint>
        {
            Ingress = FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0),
        })));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => diff.SpaceFor((PolicyDiffKind)99));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => diff.CellsFor((PolicyDiffKind)99));
        Assert.ThrowsExactly<ArgumentException>(() => diff.Classify(PolicyDiffSupport.Point(protocol: new ProtocolSymbol("nope"))));
        Assert.ThrowsExactly<ArgumentException>(() => diff.Classify(PolicyDiffSupport.Point(ingress: null, ingressSpecified: true)));
        DecisionProvenance.DefaultPolicy inputDefault = new(TrafficChain.Input);
        Assert.ThrowsExactly<ArgumentNullException>(() => new PolicyDiffCell<uint>(
            null!,
            PolicyDecision.Allow,
            PolicyDecision.Deny,
            inputDefault,
            inputDefault));
    }

    private static void AssertDiff(PolicyDiff<uint> diff, PolicyWorld<uint> baseline, PolicyWorld<uint> candidate, PolicyQuery<uint> query)
    {
        PolicyPartition<uint> baselinePartition = PolicyEvaluator.Evaluate(baseline, query);
        PolicyPartition<uint> candidatePartition = PolicyEvaluator.Evaluate(candidate, query);
        Assert.AreEqual(baselinePartition.Cardinality, diff.Cardinality);
        Assert.AreEqual(candidatePartition.Cardinality, diff.Cardinality);
        Assert.AreEqual(query.Chain, diff.Chain);
        Assert.AreEqual(IpFamily.IPv4, diff.Family);
        PolicyDiffSupport.AssertDiffInvariants(diff);
    }

    private static PolicyDiffCell<uint> Classified(
        PolicyDiff<uint> diff,
        PolicyWorld<uint> baseline,
        PolicyWorld<uint> candidate,
        PolicyQuery<uint> query,
        PacketPoint<uint> point)
    {
        PolicyPartition<uint> baselinePartition = PolicyEvaluator.Evaluate(baseline, query);
        PolicyPartition<uint> candidatePartition = PolicyEvaluator.Evaluate(candidate, query);
        PolicyDiffSupport.AssertAgrees(diff, baselinePartition, candidatePartition, baseline, candidate, point);
        PolicyDiffCell<uint>? cell = diff.Classify(point);
        Assert.IsNotNull(cell);
        return cell;
    }

    private static void AssertRuledPoint(
        PolicyDiff<uint> diff,
        PolicyWorld<uint> baseline,
        PolicyWorld<uint> candidate,
        PolicyQuery<uint> query,
        PacketPoint<uint> point,
        PolicyDiffKind kind,
        string baselineRule,
        int baselineOrder,
        string candidateRule,
        int candidateOrder)
    {
        PolicyDiffCell<uint> cell = Classified(diff, baseline, candidate, query, point);
        Assert.AreEqual(kind, cell.Difference);
        DecisionProvenance.RuleMatch baselineMatch = (DecisionProvenance.RuleMatch)cell.BaselineProvenance;
        DecisionProvenance.RuleMatch candidateMatch = (DecisionProvenance.RuleMatch)cell.CandidateProvenance;
        Assert.AreEqual(baselineRule, baselineMatch.Id.Value);
        Assert.AreEqual(baselineOrder, baselineMatch.FamilyOrder);
        Assert.AreEqual(candidateRule, candidateMatch.Id.Value);
        Assert.AreEqual(candidateOrder, candidateMatch.FamilyOrder);
    }

    private static void AssertDefaultPoint(
        PolicyDiff<uint> diff,
        PolicyWorld<uint> baseline,
        PolicyWorld<uint> candidate,
        PolicyQuery<uint> query,
        PacketPoint<uint> point,
        PolicyDiffKind kind,
        PolicyDecision baselineDecision,
        PolicyDecision candidateDecision)
    {
        PolicyDiffCell<uint> cell = Classified(diff, baseline, candidate, query, point);
        Assert.AreEqual(kind, cell.Difference);
        Assert.AreEqual(baselineDecision, cell.BaselineDecision);
        Assert.AreEqual(candidateDecision, cell.CandidateDecision);
        Assert.IsInstanceOfType<DecisionProvenance.DefaultPolicy>(cell.BaselineProvenance);
        Assert.IsInstanceOfType<DecisionProvenance.DefaultPolicy>(cell.CandidateProvenance);
        Assert.AreEqual(cell.BaselineProvenance, cell.CandidateProvenance);
    }

    private static void AssertDefaultToRule(
        PolicyDiff<uint> diff,
        PolicyWorld<uint> baseline,
        PolicyWorld<uint> candidate,
        PolicyQuery<uint> query,
        PacketPoint<uint> point,
        PolicyDiffKind kind,
        PolicyDecision candidateDecision,
        string candidateRule,
        int candidateOrder)
    {
        PolicyDiffCell<uint> cell = Classified(diff, baseline, candidate, query, point);
        Assert.AreEqual(kind, cell.Difference);
        Assert.AreEqual(candidateDecision, cell.CandidateDecision);
        Assert.IsInstanceOfType<DecisionProvenance.DefaultPolicy>(cell.BaselineProvenance);
        DecisionProvenance.RuleMatch match = (DecisionProvenance.RuleMatch)cell.CandidateProvenance;
        Assert.AreEqual(candidateRule, match.Id.Value);
        Assert.AreEqual(candidateOrder, match.FamilyOrder);
    }

    private static PolicyConstraint<uint> Narrow(IntervalSet<ushort> destinationPorts) => new()
    {
        Source = IntervalSet<uint>.Singleton(PolicyDiffSupport.Host("10.1.1.1")),
        SourcePorts = PacketPorts.Parse("40000"),
        Destination = IntervalSet<uint>.Singleton(PolicyDiffSupport.Host("192.168.0.1")),
        DestinationPorts = destinationPorts,
        Protocols = Tcp(),
        Ingress = FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0),
    };

    private static PacketPoint<uint> NarrowPoint(ushort destinationPort, uint? source = null) => PolicyDiffSupport.Point(
        source: source ?? PolicyDiffSupport.Host("10.1.1.1"),
        sourcePort: 40000,
        destination: PolicyDiffSupport.Host("192.168.0.1"),
        destinationPort: destinationPort,
        ingress: PolicyDiffSupport.Eth0);

    private static PacketPoint<uint> Port(ushort destinationPort, ProtocolSymbol? protocol = null) =>
        PolicyDiffSupport.Point(destinationPort: destinationPort, protocol: protocol);

    private static FiniteSet<ProtocolSymbol> Tcp() => FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp);

    private static PolicyRuleDefinition<uint>[] RandomRules(Random random, int count, string idPrefix)
    {
        PolicyRuleDefinition<uint>[] rules = new PolicyRuleDefinition<uint>[count];
        for (int index = 0; index < count; index++)
        {
            rules[index] = RandomRule(random, index, idPrefix);
        }

        return rules;
    }

    private static PolicyRuleDefinition<uint> RandomRule(Random random, int index, string idPrefix)
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
            : FiniteSet<NetworkInterfaceName>.Of(random.Next(0, 2) == 0 ? PolicyDiffSupport.Eth0 : PolicyDiffSupport.Eth1);
        FiniteSet<NetworkInterfaceName>? egress = chain == TrafficChain.Input || random.Next(0, 2) == 0
            ? null
            : FiniteSet<NetworkInterfaceName>.Of(random.Next(0, 2) == 0 ? PolicyDiffSupport.Eth0 : PolicyDiffSupport.Eth1);
        return PolicyDiffSupport.Rule($"{idPrefix}-r{index}", chain, decision, source: source, destinationPorts: destinationPorts, protocols: protocols, ingress: ingress, egress: egress);
    }

    private static PacketPoint<uint> RandomPoint(Random random, TrafficChain chain)
    {
        ChainProfile profile = ChainProfile.For(chain);
        return PolicyDiffSupport.Point(
            source: (uint)random.NextInt64(0, 1L << 32),
            sourcePort: (ushort)random.Next(1, 65536),
            destination: (uint)random.NextInt64(0, 1L << 32),
            destinationPort: (ushort)random.Next(1, 65536),
            protocol: random.Next(0, 2) == 0 ? ProtocolSymbol.Tcp : ProtocolSymbol.Udp,
            ingress: profile.HasIngress ? (random.Next(0, 2) == 0 ? PolicyDiffSupport.Eth0 : PolicyDiffSupport.Eth1) : null,
            egress: profile.HasEgress ? (random.Next(0, 2) == 0 ? PolicyDiffSupport.Eth0 : PolicyDiffSupport.Eth1) : null,
            ingressSpecified: true,
            egressSpecified: true);
    }
}
