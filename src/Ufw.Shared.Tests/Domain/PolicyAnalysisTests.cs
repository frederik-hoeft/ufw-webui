using System.Numerics;
using Ufw.Shared.Domain;
using Ufw.Shared.Domain.Algebra;

namespace Ufw.Shared.Tests.Domain;

[TestClass]
public sealed class PolicyAnalysisTests
{
    [TestMethod]
    public void ShadowedRules_ReportsOnlyFullyCoveredMatches()
    {
        PolicyWorld<uint> world = World(
            Rule("all-tcp", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp)),
            Rule("ssh", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("22")),
            Rule("dns", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Udp), destinationPorts: PacketPorts.Parse("53")),
            Rule("empty", TrafficChain.Input, PolicyDecision.Deny, ingress: FiniteSet<NetworkInterfaceName>.Empty),
            Rule("wide-output", TrafficChain.Output, PolicyDecision.Allow));

        IReadOnlyList<PolicyRule<uint>> shadowed = PolicyAnalysis.ShadowedRules(world, TrafficChain.Input);
        IReadOnlyList<PolicyRule<uint>> ineffective = PolicyAnalysis.IneffectiveRules(world, TrafficChain.Input);

        Assert.HasCount(1, shadowed);
        Assert.AreEqual("ssh", shadowed[0].Id.Value);
        Assert.HasCount(1, ineffective);
        Assert.AreEqual("empty", ineffective[0].Id.Value);
        Assert.AreEqual(BigInteger.Zero, PolicyAnalysis.ShadowedPortion(world, world.Rules[2]).Cardinality);
        Assert.AreEqual(world.Rules[1].Match.Cardinality, PolicyAnalysis.ShadowedPortion(world, world.Rules[1]).Cardinality);
    }

    [TestMethod]
    public void ShadowedPortion_IsTheOverlapClaimedByAnEarlierRule()
    {
        PolicyWorld<uint> world = World(
            Rule("ssh", TrafficChain.Input, PolicyDecision.Reject, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), destinationPorts: PacketPorts.Parse("22")),
            Rule("all-tcp", TrafficChain.Input, PolicyDecision.Allow, protocols: FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp)));
        PacketSpace<uint> shadowed = PolicyAnalysis.ShadowedPortion(world, world.Rules[1]);

        Assert.AreEqual(FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp), shadowed.ProjectProtocols());
        Assert.AreEqual(PacketPorts.Parse("22"), shadowed.ProjectDestinationPorts());
        Assert.AreEqual(world.Rules[0].Match.Cardinality, shadowed.Cardinality);
        Assert.IsTrue(PolicyAnalysis.ShadowedRules(world, TrafficChain.Input).Count == 0);
    }

    private static PolicyWorld<uint> World(params PolicyRuleDefinition<uint>[] rules) =>
        PolicyWorld.CreateIPv4(
            FiniteSet<ProtocolSymbol>.Of(ProtocolSymbol.Tcp, ProtocolSymbol.Udp),
            FiniteSet<NetworkInterfaceName>.Of(new NetworkInterfaceName("eth0")),
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            PolicyDecision.Deny,
            rules);

    private static PolicyRuleDefinition<uint> Rule(
        string id,
        TrafficChain chain,
        PolicyDecision decision,
        IntervalSet<ushort>? destinationPorts = null,
        FiniteSet<ProtocolSymbol>? protocols = null,
        FiniteSet<NetworkInterfaceName>? ingress = null) => new()
        {
            Id = new RuleId(id),
            Chain = chain,
            Decision = decision,
            DestinationPorts = destinationPorts,
            Protocols = protocols,
            Ingress = ingress,
        };
}
