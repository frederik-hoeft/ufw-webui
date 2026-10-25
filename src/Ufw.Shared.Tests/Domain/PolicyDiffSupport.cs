using System.Numerics;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

internal static class PolicyDiffSupport
{
    internal static readonly NetworkInterfaceName s_eth0 = new("eth0");
    internal static readonly NetworkInterfaceName s_eth1 = new("eth1");

    internal static PolicyWorld<uint> World(
        PolicyRuleDefinition<uint>[] rules,
        PolicyDecision incoming = PolicyDecision.Deny,
        PolicyDecision outgoing = PolicyDecision.Allow,
        PolicyDecision routed = PolicyDecision.Reject,
        IReadOnlyCollection<ProtocolDefinition>? protocols = null,
        FiniteSet<NetworkInterfaceName>? interfaces = null) =>
        PolicyWorld.CreateIPv4(
            protocols ?? [ProtocolDefinition.Tcp, ProtocolDefinition.Udp],
            interfaces ?? FiniteSet<NetworkInterfaceName>.Of(s_eth0, s_eth1),
            incoming,
            outgoing,
            routed,
            rules);

    internal static PolicyRuleDefinition<uint> Rule(
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

    internal static PolicyQuery<uint> Query(TrafficChain chain, PolicyConstraint<uint>? constraint = null) =>
        PolicyQuery<uint>.Create(IpFamily.IPv4, chain, constraint);

    internal static PacketPoint<uint> Point(
        uint source = 0x0a000001,
        ushort? sourcePort = 40000,
        uint destination = 0xc0a80001,
        ushort? destinationPort = 80,
        ProtocolSymbol? protocol = null,
        NetworkInterfaceName? ingress = null,
        NetworkInterfaceName? egress = null,
        bool ingressSpecified = false,
        bool egressSpecified = false) => new(
            source,
            sourcePort,
            destination,
            destinationPort,
            protocol ?? ProtocolSymbol.Tcp,
            ingressSpecified ? ingress : ingress ?? s_eth0,
            egressSpecified ? egress : egress);

    internal static uint Host(string address) => NetworkAddress.ParseIPv4(address).Start;

    internal static void AssertDiffInvariants(PolicyDiff<uint> diff)
    {
        BigInteger sum = BigInteger.Zero;
        BigInteger decision = BigInteger.Zero;
        BigInteger responsibility = BigInteger.Zero;
        BigInteger unchanged = BigInteger.Zero;
        BigInteger changed = BigInteger.Zero;
        BigInteger both = BigInteger.Zero;
        for (int index = 0; index < diff.Cells.Count; index++)
        {
            PolicyDiffCell<uint> cell = diff.Cells[index];
            Assert.IsGreaterThan(BigInteger.Zero, cell.Region.Cardinality);
            sum += cell.Region.Cardinality;
            bool decisionChanged = cell.BaselineDecision != cell.CandidateDecision;
            bool responsibilityChanged = ResponsibilityDiffers(cell.BaselineProvenance, cell.CandidateProvenance);
            Assert.AreEqual(decisionChanged, cell.DecisionChanged);
            Assert.AreEqual(responsibilityChanged, cell.ResponsibilityChanged);
            Assert.AreEqual(responsibilityChanged, cell.BaselineResponsibility != cell.CandidateResponsibility);
            Assert.AreEqual(KindOf(decisionChanged, responsibilityChanged), cell.Difference);
            for (int other = index + 1; other < diff.Cells.Count; other++)
            {
                Assert.IsLessThan(0, cell.Region.CompareTo(diff.Cells[other].Region));
                Assert.IsFalse(cell.Region.Overlaps(diff.Cells[other].Region));
            }

            if (decisionChanged)
            {
                decision += cell.Region.Cardinality;
            }

            if (responsibilityChanged)
            {
                responsibility += cell.Region.Cardinality;
            }

            if (decisionChanged && responsibilityChanged)
            {
                both += cell.Region.Cardinality;
            }

            if (!decisionChanged && !responsibilityChanged)
            {
                unchanged += cell.Region.Cardinality;
            }
            else
            {
                changed += cell.Region.Cardinality;
            }
        }

        Assert.AreEqual(diff.Cardinality, sum);
        Assert.AreEqual(decision, diff.DecisionChanges().Cardinality);
        Assert.AreEqual(responsibility, diff.ResponsibilityShifts().Cardinality);
        Assert.AreEqual(unchanged, diff.Unchanged().Cardinality);
        Assert.AreEqual(changed, diff.Changed().Cardinality);
        Assert.AreEqual(diff.Cardinality, changed + unchanged);
        Assert.IsTrue(diff.Changed().Intersect(diff.Unchanged()).IsEmpty);
        Assert.IsTrue(diff.Changed().SetEquals(diff.DecisionChanges().Union(diff.ResponsibilityShifts())));
        Assert.AreEqual(both, diff.DecisionChanges().Intersect(diff.ResponsibilityShifts()).Cardinality);
        Assert.AreEqual(both, diff.SpaceFor(PolicyDiffKind.Both).Cardinality);
        Assert.AreEqual(unchanged, diff.SpaceFor(PolicyDiffKind.Unchanged).Cardinality);
        Assert.AreEqual(
            diff.Cardinality,
            diff.SpaceFor(PolicyDiffKind.Unchanged).Cardinality
                + diff.SpaceFor(PolicyDiffKind.DecisionOnly).Cardinality
                + diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly).Cardinality
                + diff.SpaceFor(PolicyDiffKind.Both).Cardinality);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.DecisionOnly).Intersect(diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly)).IsEmpty);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.DecisionOnly).Intersect(diff.SpaceFor(PolicyDiffKind.Both)).IsEmpty);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.ResponsibilityOnly).Intersect(diff.SpaceFor(PolicyDiffKind.Both)).IsEmpty);
        Assert.IsTrue(diff.SpaceFor(PolicyDiffKind.Unchanged).Intersect(diff.SpaceFor(PolicyDiffKind.Both)).IsEmpty);
    }

    internal static void AssertAgrees(
        PolicyDiff<uint> diff,
        PolicyPartition<uint> baseline,
        PolicyPartition<uint> candidate,
        PolicyWorld<uint> baselineWorld,
        PolicyWorld<uint> candidateWorld,
        PacketPoint<uint> point)
    {
        PolicyCell<uint>? leftCell = baseline.Decide(point);
        PolicyCell<uint>? rightCell = candidate.Decide(point);
        PolicyDiffCell<uint>? classified = diff.Classify(point);
        (PolicyDecision leftDecision, DecisionProvenance leftProvenance) = Brute(baselineWorld, baseline.Chain, point);
        (PolicyDecision rightDecision, DecisionProvenance rightProvenance) = Brute(candidateWorld, candidate.Chain, point);
        if (leftCell is null || rightCell is null)
        {
            Assert.IsNull(leftCell);
            Assert.IsNull(rightCell);
            Assert.IsNull(classified);
            return;
        }

        Assert.IsNotNull(classified);
        Assert.AreEqual(leftDecision, leftCell.Decision);
        Assert.AreEqual(leftProvenance, leftCell.Provenance);
        Assert.AreEqual(rightDecision, rightCell.Decision);
        Assert.AreEqual(rightProvenance, rightCell.Provenance);
        Assert.AreEqual(leftCell.Decision, classified.BaselineDecision);
        Assert.AreEqual(rightCell.Decision, classified.CandidateDecision);
        Assert.AreEqual(leftCell.Provenance, classified.BaselineProvenance);
        Assert.AreEqual(rightCell.Provenance, classified.CandidateProvenance);
        Assert.AreEqual(leftDecision != rightDecision, classified.DecisionChanged);
        Assert.AreEqual(ResponsibilityDiffers(leftProvenance, rightProvenance), classified.ResponsibilityChanged);
        Assert.IsTrue(classified.Region.Contains(point));
        Assert.AreEqual(baseline.Chain, diff.Chain);
        Assert.AreEqual(candidate.Chain, diff.Chain);
    }

    internal static void AssertSameDiff(PolicyDiff<uint> left, PolicyDiff<uint> right)
    {
        Assert.AreEqual(left.Chain, right.Chain);
        Assert.AreEqual(left.Family, right.Family);
        Assert.AreEqual(left.Cardinality, right.Cardinality);
        Assert.HasCount(left.Cells.Count, right.Cells);
        for (int index = 0; index < left.Cells.Count; index++)
        {
            PolicyDiffCell<uint> leftCell = left.Cells[index];
            PolicyDiffCell<uint> rightCell = right.Cells[index];
            Assert.AreEqual(leftCell.Region, rightCell.Region);
            Assert.AreEqual(leftCell.BaselineDecision, rightCell.BaselineDecision);
            Assert.AreEqual(leftCell.CandidateDecision, rightCell.CandidateDecision);
            Assert.AreEqual(leftCell.BaselineProvenance, rightCell.BaselineProvenance);
            Assert.AreEqual(leftCell.CandidateProvenance, rightCell.CandidateProvenance);
        }
    }

    internal static (PolicyDecision Decision, DecisionProvenance Provenance) Brute(
        PolicyWorld<uint> world,
        TrafficChain chain,
        PacketPoint<uint> point)
    {
        foreach (PolicyRule<uint> rule in world.Rules)
        {
            if (rule.Chain != chain || !rule.Matches(point))
            {
                continue;
            }

            return (rule.Decision, new DecisionProvenance.RuleMatch(rule.Id, rule.FamilyOrder));
        }

        return (world.DefaultFor(chain), new DecisionProvenance.DefaultPolicy(chain));
    }

    /// <summary>
    /// Oracle that does not call <see cref="DecisionProvenance.HasSameResponsibilityAs"/>.
    /// Family order is intentionally unread.
    /// </summary>
    internal static bool ResponsibilityDiffers(DecisionProvenance left, DecisionProvenance right)
    {
        if (left is DecisionProvenance.RuleMatch leftRule && right is DecisionProvenance.RuleMatch rightRule)
        {
            return !string.Equals(leftRule.Id.Value, rightRule.Id.Value, StringComparison.Ordinal);
        }

        if (left is DecisionProvenance.DefaultPolicy leftDefault && right is DecisionProvenance.DefaultPolicy rightDefault)
        {
            return leftDefault.Chain != rightDefault.Chain;
        }

        return true;
    }

    private static PolicyDiffKind KindOf(bool decisionChanged, bool responsibilityChanged) => (decisionChanged, responsibilityChanged) switch
    {
        (false, false) => PolicyDiffKind.Unchanged,
        (true, false) => PolicyDiffKind.DecisionOnly,
        (false, true) => PolicyDiffKind.ResponsibilityOnly,
        _ => PolicyDiffKind.Both,
    };
}
