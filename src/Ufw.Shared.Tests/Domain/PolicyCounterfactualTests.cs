using System.Numerics;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class PolicyCounterfactualTests
{
    [TestMethod]
    public void Remove_LeavesTheInputWorldUnchangedAndMatchesARebuiltCandidate()
    {
        PolicyRuleDefinition<uint> ssh = Rule("ssh", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> web = Rule("web", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("80"));
        PolicyWorld<uint> world = PolicyDiffSupport.World([ssh, web]);
        PolicyRule<uint>[] before = [.. world.Rules];
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> impact = PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[0], query);
        PolicyWorld<uint> rebuilt = PolicyDiffSupport.World([web]);
        PolicyWorld<uint> derived = world.WithoutOccurrence(before[0]);

        Assert.HasCount(2, world.Rules);
        Assert.AreSame(before[0], world.Rules[0]);
        Assert.AreSame(before[1], world.Rules[1]);
        Assert.AreEqual(0, world.Rules[0].FamilyOrder);
        Assert.AreEqual(1, world.Rules[1].FamilyOrder);
        Assert.AreEqual(1, derived.Rules.Count);
        Assert.AreNotSame(before[1], derived.Rules[0]);
        Assert.AreEqual(before[1].Id, derived.Rules[0].Id);
        Assert.AreEqual(0, derived.Rules[0].FamilyOrder);
        Assert.AreEqual(before[1].Match.Cardinality, derived.Rules[0].Match.Cardinality);
        PolicyDiffSupport.AssertSameDiff(impact, PolicyDiffer.Compare(world, rebuilt, query));
        PolicyDiffSupport.AssertSameDiff(impact, PolicyDiffer.Compare(world, derived, query));
        AssertSamePartition(PolicyEvaluator.Evaluate(derived, query), PolicyEvaluator.Evaluate(rebuilt, query));
        PolicyDiff<uint> removedWeb = PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[1], query);
        Assert.HasCount(2, world.Rules);
        Assert.AreEqual(PolicyDiffKind.Both, impact.Classify(Port(22))!.Difference);
        Assert.AreEqual(PolicyDiffKind.Unchanged, removedWeb.Classify(Port(22))!.Difference);
        Assert.AreEqual(PolicyDiffKind.Both, removedWeb.Classify(Port(80))!.Difference);
    }

    [TestMethod]
    public void Remove_ReportsTheHandoffToTheNextRuleOrDefault()
    {
        PolicyWorld<uint> world = PolicyDiffSupport.World(
        [
            Rule("ssh", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            Rule("net10", PolicyDecision.Deny, source: IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8"))),
        ]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[0], query);

        AssertImpact(diff, world, world.Rules[0], query);
        AssertPoint(diff, world, world.Rules[0], query, Port(22, source: PolicyDiffSupport.Host("11.0.0.1")), PolicyDiffKind.Both, PolicyDecision.Allow, PolicyDecision.Deny);
        AssertPoint(diff, world, world.Rules[0], query, Port(22, source: PolicyDiffSupport.Host("10.1.1.1")), PolicyDiffKind.Both, PolicyDecision.Allow, PolicyDecision.Deny);
        PolicyDiffCell<uint> handedToNet = Classified(diff, world, world.Rules[0], query, Port(22, source: PolicyDiffSupport.Host("10.1.1.1")));
        Assert.AreEqual("net10", ((DecisionProvenance.RuleMatch)handedToNet.CandidateProvenance).Id.Value);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)handedToNet.CandidateProvenance).FamilyOrder);
        PolicyDiffCell<uint> stillNet = Classified(diff, world, world.Rules[0], query, Port(80, source: PolicyDiffSupport.Host("10.1.1.1")));
        Assert.AreEqual(PolicyDiffKind.Unchanged, stillNet.Difference);
        Assert.AreEqual(PolicyDecision.Deny, stillNet.BaselineDecision);
        DecisionProvenance.RuleMatch baselineNet = (DecisionProvenance.RuleMatch)stillNet.BaselineProvenance;
        DecisionProvenance.RuleMatch candidateNet = (DecisionProvenance.RuleMatch)stillNet.CandidateProvenance;
        Assert.AreEqual("net10", baselineNet.Id.Value);
        Assert.AreEqual("net10", candidateNet.Id.Value);
        Assert.AreEqual(1, baselineNet.FamilyOrder);
        Assert.AreEqual(0, candidateNet.FamilyOrder);
        Assert.AreNotEqual(stillNet.BaselineProvenance, stillNet.CandidateProvenance);
        AssertPoint(diff, world, world.Rules[0], query, Port(80, source: PolicyDiffSupport.Host("11.0.0.1")), PolicyDiffKind.Unchanged, PolicyDecision.Deny, PolicyDecision.Deny);
    }

    [TestMethod]
    public void Remove_SameActionAsTheDefault_IsResponsibilityOnly()
    {
        PolicyWorld<uint> world = PolicyDiffSupport.World(
            [Rule("redundant", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"))],
            incoming: PolicyDecision.Allow);
        PolicyRule<uint> occurrence = world.Rules[0];
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyCounterfactual.ImpactOfRemoving(world, occurrence, query);

        AssertImpact(diff, world, occurrence, query);
        Assert.IsTrue(diff.DecisionChanges().IsEmpty);
        Assert.AreEqual(Tcp(), diff.ResponsibilityShifts().ProjectProtocols());
        Assert.AreEqual(PacketPortSet.FromPorts(PacketPorts.Parse("22")), diff.ResponsibilityShifts().ProjectDestinationPorts());
        AssertPoint(diff, world, occurrence, query, Port(22), PolicyDiffKind.ResponsibilityOnly, PolicyDecision.Allow, PolicyDecision.Allow);
        AssertPoint(diff, world, occurrence, query, Port(80), PolicyDiffKind.Unchanged, PolicyDecision.Allow, PolicyDecision.Allow);
        Assert.IsInstanceOfType<DecisionProvenance.RuleMatch>(Classified(diff, world, occurrence, query, Port(22)).BaselineProvenance);
        Assert.IsInstanceOfType<DecisionProvenance.DefaultPolicy>(Classified(diff, world, occurrence, query, Port(22)).CandidateProvenance);
    }

    [TestMethod]
    public void Remove_ShadowedOccurrence_HasNoSemanticImpactButRenumbersLaterRules()
    {
        PolicyWorld<uint> world = PolicyDiffSupport.World(
        [
            Rule("wide", PolicyDecision.Allow, protocols: Tcp()),
            Rule("ssh", PolicyDecision.Deny, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            Rule("dns", PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Udp), destinationPorts: PacketPorts.Parse("53")),
        ]);
        PolicyRule<uint> shadowed = world.Rules[1];
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> diff = PolicyCounterfactual.ImpactOfRemoving(world, shadowed, query);

        AssertImpact(diff, world, shadowed, query);
        Assert.IsTrue(diff.Changed().IsEmpty);
        Assert.AreEqual(diff.Cardinality, diff.Unchanged().Cardinality);
        PolicyDiffCell<uint> tcp = Classified(diff, world, shadowed, query, Port(22));
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)tcp.BaselineProvenance).FamilyOrder);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)tcp.CandidateProvenance).FamilyOrder);
        Assert.AreEqual(tcp.BaselineProvenance, tcp.CandidateProvenance);
        PolicyDiffCell<uint> dns = Classified(diff, world, shadowed, query, Port(53, protocol: ProtocolSymbol.Udp));
        Assert.AreEqual(PolicyDiffKind.Unchanged, dns.Difference);
        Assert.AreEqual(2, ((DecisionProvenance.RuleMatch)dns.BaselineProvenance).FamilyOrder);
        Assert.AreEqual(1, ((DecisionProvenance.RuleMatch)dns.CandidateProvenance).FamilyOrder);
        Assert.IsFalse(dns.ResponsibilityChanged);
    }

    [TestMethod]
    public void Remove_OtherChainRule_DoesNotChangeTheQueriedChain()
    {
        PolicyWorld<uint> world = PolicyDiffSupport.World(
        [
            Rule("in", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            PolicyDiffSupport.Rule("out", TrafficChain.Output, PolicyDecision.Deny),
        ]);
        PolicyQuery<uint> input = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyQuery<uint> output = PolicyDiffSupport.Query(TrafficChain.Output);
        PolicyDiff<uint> inputImpact = PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[1], input);
        PolicyDiff<uint> outputImpact = PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[0], output);

        AssertImpact(inputImpact, world, world.Rules[1], input);
        AssertImpact(outputImpact, world, world.Rules[0], output);
        Assert.IsTrue(inputImpact.Changed().IsEmpty);
        PolicyDiffCell<uint> ssh = Classified(inputImpact, world, world.Rules[1], input, Port(22));
        Assert.AreEqual(ssh.BaselineProvenance, ssh.CandidateProvenance);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)ssh.BaselineProvenance).FamilyOrder);
        Assert.IsTrue(outputImpact.DecisionChanges().IsEmpty);
        Assert.IsTrue(outputImpact.ResponsibilityShifts().IsEmpty);
        PacketPoint<uint> outputPoint = PolicyDiffSupport.Point(ingress: null, ingressSpecified: true, egress: PolicyDiffSupport.Eth0);
        PolicyDiffCell<uint> outputCell = Classified(outputImpact, world, world.Rules[0], output, outputPoint);
        Assert.AreEqual(PolicyDiffKind.Unchanged, outputCell.Difference);
        Assert.AreEqual(PolicyDecision.Deny, outputCell.BaselineDecision);
        Assert.AreEqual(1, ((DecisionProvenance.RuleMatch)outputCell.BaselineProvenance).FamilyOrder);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)outputCell.CandidateProvenance).FamilyOrder);
        Assert.AreEqual("out", ((DecisionProvenance.RuleMatch)outputCell.CandidateProvenance).Id.Value);
    }

    [TestMethod]
    public void Remove_DuplicateSemanticRules_UsesSemanticEquality()
    {
        PolicyRuleDefinition<uint> first = Rule("dup", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyRuleDefinition<uint> second = Rule("dup", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22"));
        PolicyWorld<uint> sameAction = PolicyDiffSupport.World([first, second]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);
        PolicyDiff<uint> noImpact = PolicyCounterfactual.ImpactOfRemoving(sameAction, sameAction.Rules[0], query);

        AssertImpact(noImpact, sameAction, sameAction.Rules[0], query);
        Assert.IsTrue(noImpact.Changed().IsEmpty);
        PolicyDiffCell<uint> stillDup = Classified(noImpact, sameAction, sameAction.Rules[0], query, Port(22));
        Assert.AreEqual(stillDup.BaselineProvenance, stillDup.CandidateProvenance);
        Assert.AreEqual("dup", ((DecisionProvenance.RuleMatch)stillDup.CandidateProvenance).Id.Value);

        PolicyWorld<uint> differentAction = PolicyDiffSupport.World(
        [
            Rule("dup", PolicyDecision.Allow, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            Rule("dup-deny", PolicyDecision.Deny, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
        ]);
        PolicyRule<uint> allowing = differentAction.Rules[0];
        PolicyDiff<uint> bothChanged = PolicyCounterfactual.ImpactOfRemoving(differentAction, allowing, query);
        AssertImpact(bothChanged, differentAction, allowing, query);
        AssertPoint(bothChanged, differentAction, allowing, query, Port(22), PolicyDiffKind.Both, PolicyDecision.Allow, PolicyDecision.Deny);
        Assert.IsTrue(bothChanged.ResponsibilityShifts().SetEquals(bothChanged.DecisionChanges()));
        PolicyDiffCell<uint> replaced = Classified(bothChanged, differentAction, allowing, query, Port(22));
        Assert.AreNotEqual(replaced.BaselineProvenance, replaced.CandidateProvenance);
        Assert.AreEqual("dup-deny", ((DecisionProvenance.RuleMatch)replaced.CandidateProvenance).Id.Value);
        Assert.AreEqual(0, ((DecisionProvenance.RuleMatch)replaced.CandidateProvenance).FamilyOrder);
    }

    [TestMethod]
    public void Remove_EffectiveNarrowRule_WithBroaderSameDecisionFallback_ShiftsResponsibilityOnly()
    {
        IntervalSet<uint> narrower = IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/9"));
        IntervalSet<uint> broader = IntervalSet<uint>.Of(NetworkAddress.ParseIPv4("10.0.0.0/8"));
        PolicyWorld<uint> world = PolicyDiffSupport.World(
        [
            Rule("narrow", PolicyDecision.Allow, source: narrower, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
            Rule("broad", PolicyDecision.Allow, source: broader, protocols: Tcp(), destinationPorts: PacketPorts.Parse("22")),
        ]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input, new PolicyConstraint<uint>
        {
            Source = broader,
            DestinationPorts = PacketPorts.Parse("22"),
            Protocols = Tcp(),
        });
        PolicyDiff<uint> impact = PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[0], query);

        AssertImpact(impact, world, world.Rules[0], query);
        Assert.IsTrue(impact.DecisionChanges().IsEmpty);
        Assert.AreEqual(narrower, impact.ResponsibilityShifts().ProjectSourceAddresses());
        AssertPoint(impact, world, world.Rules[0], query, Port(22, source: PolicyDiffSupport.Host("10.1.1.1")), PolicyDiffKind.ResponsibilityOnly, PolicyDecision.Allow, PolicyDecision.Allow);
        AssertPoint(impact, world, world.Rules[0], query, Port(22, source: PolicyDiffSupport.Host("10.200.1.1")), PolicyDiffKind.Unchanged, PolicyDecision.Allow, PolicyDecision.Allow);
        Assert.AreSame(world.Rules[0], world.Rules[0]);
        Assert.HasCount(2, world.Rules);
    }

    [TestMethod]
    public void Remove_IPv6Prefix_ReturnsThatPrefixToTheDefault()
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
            FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0),
            PolicyDecision.Deny,
            PolicyDecision.Allow,
            PolicyDecision.Reject,
            [rule]);
        PolicyRule<UInt128> occurrence = world.Rules[0];
        PolicyQuery<UInt128> query = PolicyQuery<UInt128>.Create(IpFamily.IPv6, TrafficChain.Input);
        PolicyDiff<UInt128> diff = PolicyCounterfactual.ImpactOfRemoving(world, occurrence, query);
        UInt128 inside = NetworkAddress.ParseIPv6("2001:db8::1").Start;
        UInt128 outside = NetworkAddress.ParseIPv6("2001:db9::1").Start;
        PacketPoint<UInt128> insidePoint = new(UInt128.Zero, 1, inside, 443, ProtocolSymbol.Tcp, PolicyDiffSupport.Eth0);
        PacketPoint<UInt128> outsidePoint = new(UInt128.Zero, 1, outside, 443, ProtocolSymbol.Tcp, PolicyDiffSupport.Eth0);

        Assert.AreSame(occurrence, world.Rules[0]);
        Assert.HasCount(1, world.Rules);
        Assert.AreEqual(IntervalSet<UInt128>.Of(prefix), diff.DecisionChanges().ProjectDestinationAddresses());
        Assert.AreEqual(world.AddressUniverse.Except(IntervalSet<UInt128>.Of(prefix)), diff.Unchanged().ProjectDestinationAddresses());
        Assert.AreEqual(diff.DecisionChanges().Cardinality, diff.ResponsibilityShifts().Cardinality);
        Assert.AreEqual(PolicyDiffKind.Both, diff.Classify(insidePoint)!.Difference);
        Assert.AreEqual(PolicyDecision.Allow, diff.Classify(insidePoint)!.BaselineDecision);
        Assert.AreEqual(PolicyDecision.Deny, diff.Classify(insidePoint)!.CandidateDecision);
        Assert.AreEqual(PolicyDiffKind.Unchanged, diff.Classify(outsidePoint)!.Difference);
        Assert.AreEqual(PolicyEvaluator.Evaluate(world, query).Cardinality, diff.Cardinality);
        Assert.AreEqual(BigInteger.Zero, world.WithoutOccurrence(occurrence).Rules.Count);
    }

    [TestMethod]
    public void Remove_EveryRandomOccurrence_AgreesWithTheRebuiltWorld()
    {
        Random random = new(20260329);
        PolicyRuleDefinition<uint>[] rules = new PolicyRuleDefinition<uint>[5];
        for (int index = 0; index < rules.Length; index++)
        {
            TrafficChain chain = (TrafficChain)random.Next(0, 3);
            PolicyDecision decision = (PolicyDecision)random.Next(0, 4);
            rules[index] = PolicyDiffSupport.Rule(
                $"r{index}",
                chain,
                decision,
                destinationPorts: random.Next(0, 2) == 0 ? null : PacketPorts.Parse((random.Next(1, 1024)).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                protocols: random.Next(0, 2) == 0 ? null : Tcp(),
                ingress: chain == TrafficChain.Output ? null : FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth0),
                egress: chain == TrafficChain.Input ? null : FiniteSet<NetworkInterfaceName>.Of(PolicyDiffSupport.Eth1));
        }

        PolicyWorld<uint> world = PolicyDiffSupport.World(rules);
        PolicyRule<uint>[] before = [.. world.Rules];
        for (int index = 0; index < rules.Length; index++)
        {
            TrafficChain chain = rules[index].Chain;
            PolicyQuery<uint> query = PolicyDiffSupport.Query(chain);
            PolicyRuleDefinition<uint>[] remaining = [.. rules.Where((_, ruleIndex) => ruleIndex != index)];
            PolicyWorld<uint> rebuilt = PolicyDiffSupport.World(remaining);
            PolicyDiff<uint> impact = PolicyCounterfactual.ImpactOfRemoving(world, before[index], query);
            PolicyDiffSupport.AssertSameDiff(impact, PolicyDiffer.Compare(world, rebuilt, query));
            PolicyDiffSupport.AssertDiffInvariants(impact);
            Assert.AreSame(before[index], world.Rules[index]);
        }
    }

    [TestMethod]
    public void Remove_RejectsAForeignOccurrenceAndNullArguments()
    {
        PolicyWorld<uint> world = PolicyDiffSupport.World([Rule("ssh", PolicyDecision.Allow)]);
        PolicyWorld<uint> other = PolicyDiffSupport.World([Rule("ssh", PolicyDecision.Allow)]);
        PolicyQuery<uint> query = PolicyDiffSupport.Query(TrafficChain.Input);

        Assert.ThrowsExactly<ArgumentException>(() => world.WithoutOccurrence(other.Rules[0]));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyCounterfactual.ImpactOfRemoving(world, other.Rules[0], query));
        Assert.ThrowsExactly<ArgumentNullException>(() => world.WithoutOccurrence(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyCounterfactual.ImpactOfRemoving<uint>(null!, world.Rules[0], query));
        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyCounterfactual.ImpactOfRemoving(world, null!, query));
        Assert.ThrowsExactly<ArgumentNullException>(() => PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[0], null!));
        Assert.ThrowsExactly<ArgumentException>(() => PolicyCounterfactual.ImpactOfRemoving(world, world.Rules[0], PolicyQuery<uint>.Create(IpFamily.IPv6, TrafficChain.Input)));
        Assert.AreSame(world.Rules[0], world.Rules[0]);
        Assert.HasCount(1, world.Rules);
    }

    private static void AssertImpact(PolicyDiff<uint> diff, PolicyWorld<uint> world, PolicyRule<uint> occurrence, PolicyQuery<uint> query)
    {
        PolicyWorld<uint> candidate = world.WithoutOccurrence(occurrence);
        PolicyDiffSupport.AssertSameDiff(diff, PolicyDiffer.Compare(world, candidate, query));
        PolicyDiffSupport.AssertDiffInvariants(diff);
        Assert.AreEqual(query.Chain, diff.Chain);
    }

    private static PolicyDiffCell<uint> Classified(PolicyDiff<uint> diff, PolicyWorld<uint> world, PolicyRule<uint> occurrence, PolicyQuery<uint> query, PacketPoint<uint> point)
    {
        PolicyWorld<uint> candidate = world.WithoutOccurrence(occurrence);
        PolicyPartition<uint> baseline = PolicyEvaluator.Evaluate(world, query);
        PolicyPartition<uint> compared = PolicyEvaluator.Evaluate(candidate, query);
        PolicyDiffSupport.AssertAgrees(diff, baseline, compared, world, candidate, point);
        PolicyDiffCell<uint>? cell = diff.Classify(point);
        Assert.IsNotNull(cell);
        return cell;
    }

    private static void AssertPoint(
        PolicyDiff<uint> diff,
        PolicyWorld<uint> world,
        PolicyRule<uint> occurrence,
        PolicyQuery<uint> query,
        PacketPoint<uint> point,
        PolicyDiffKind kind,
        PolicyDecision baselineDecision,
        PolicyDecision candidateDecision)
    {
        PolicyDiffCell<uint> cell = Classified(diff, world, occurrence, query, point);
        Assert.AreEqual(kind, cell.Difference);
        Assert.AreEqual(baselineDecision, cell.BaselineDecision);
        Assert.AreEqual(candidateDecision, cell.CandidateDecision);
    }

    private static void AssertSamePartition(PolicyPartition<uint> left, PolicyPartition<uint> right)
    {
        Assert.AreEqual(left.Cardinality, right.Cardinality);
        Assert.HasCount(left.Cells.Count, right.Cells);
        for (int index = 0; index < left.Cells.Count; index++)
        {
            Assert.AreEqual(left.Cells[index].Region, right.Cells[index].Region);
            Assert.AreEqual(left.Cells[index].Decision, right.Cells[index].Decision);
            Assert.AreEqual(left.Cells[index].Provenance, right.Cells[index].Provenance);
        }
    }

    private static PolicyRuleDefinition<uint> Rule(
        string id,
        PolicyDecision decision,
        IntervalSet<uint>? source = null,
        IntervalSet<ushort>? destinationPorts = null,
        FiniteSet<ProtocolSymbol>? protocols = null) =>
        PolicyDiffSupport.Rule(id, TrafficChain.Input, decision, source: source, destinationPorts: destinationPorts, protocols: protocols);

    private static PacketPoint<uint> Port(ushort destinationPort, uint? source = null, ProtocolSymbol? protocol = null) =>
        PolicyDiffSupport.Point(source: source ?? PolicyDiffSupport.Host("10.1.1.1"), destinationPort: destinationPort, protocol: protocol);

    private static FiniteSet<ProtocolSymbol> Tcp() => FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp);
}
