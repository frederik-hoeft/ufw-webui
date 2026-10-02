using System.Diagnostics.CodeAnalysis;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected argv arrays are local one-shot test assertions.")]
public sealed class UfwInsertionPlacementResolverTests
{
    private static readonly IUfwRuleCommandRenderer s_renderer = new UfwRuleCommandRenderer();

    [TestMethod]
    public void Resolve_Ipv4BeginningAndMiddle_UsesCombinedOneBasedInsertPositions()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4, "22"),
            Opaque("[ 2] unsupported ipv4 syntax"),
            Parsed(FirewallAddressFamily.IPv6, "22"),
        ];

        UfwInsertionPlacement beginning = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv4, 0);
        UfwInsertionPlacement middle = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv4, 1);

        Assert.AreEqual(0, beginning.ExpectedOccurrenceIndex);
        Assert.AreEqual(1, beginning.UfwInsertPosition);
        Assert.AreEqual(1, middle.ExpectedOccurrenceIndex);
        Assert.AreEqual(2, middle.UfwInsertPosition);
    }

    [TestMethod]
    public void Resolve_Ipv4FamilyEnd_UsesAddAtIpv4Ipv6Boundary()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4, "22"),
            Parsed(FirewallAddressFamily.IPv4, "80"),
            Parsed(FirewallAddressFamily.IPv6, "22"),
        ];

        UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv4, 2);

        Assert.AreEqual(2, placement.ExpectedOccurrenceIndex);
        Assert.IsNull(placement.UfwInsertPosition);
    }

    [TestMethod]
    public void Resolve_Ipv6Beginning_UsesCombinedUfwNumbering()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4, "22"),
            Opaque("[ 2] unsupported ipv4 syntax"),
            Parsed(FirewallAddressFamily.IPv6, "80"),
            Opaque("[ 4] unsupported ipv6 syntax (v6)"),
        ];

        UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv6, 2);

        Assert.AreEqual(2, placement.ExpectedOccurrenceIndex);
        Assert.AreEqual(3, placement.UfwInsertPosition);
    }

    [TestMethod]
    public void Resolve_Ipv6End_UsesAdd()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4, "22"),
            Parsed(FirewallAddressFamily.IPv6, "80"),
        ];

        UfwInsertionPlacement placement = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv6, rules.Length);

        Assert.AreEqual(2, placement.ExpectedOccurrenceIndex);
        Assert.IsNull(placement.UfwInsertPosition);
    }

    [TestMethod]
    public void Resolve_EmptyFamily_UsesItsPartitionBoundary()
    {
        ListedFirewallRule[] ipv6Only = [Parsed(FirewallAddressFamily.IPv6, "22")];
        ListedFirewallRule[] ipv4Only = [Parsed(FirewallAddressFamily.IPv4, "22")];

        UfwInsertionPlacement ipv4 = UfwInsertionPlacementResolver.Resolve(ipv6Only, FirewallAddressFamily.IPv4, 0);
        UfwInsertionPlacement ipv6 = UfwInsertionPlacementResolver.Resolve(ipv4Only, FirewallAddressFamily.IPv6, 1);

        Assert.IsNull(ipv4.UfwInsertPosition);
        Assert.AreEqual(0, ipv4.ExpectedOccurrenceIndex);
        Assert.IsNull(ipv6.UfwInsertPosition);
        Assert.AreEqual(1, ipv6.ExpectedOccurrenceIndex);
    }

    [TestMethod]
    public void Resolve_RejectsCrossFamilySlotsAndMalformedPartitions()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4, "22"),
            Parsed(FirewallAddressFamily.IPv6, "22"),
        ];
        ListedFirewallRule[] malformed =
        [
            Parsed(FirewallAddressFamily.IPv6, "22"),
            Parsed(FirewallAddressFamily.IPv4, "22"),
        ];

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv4, 2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv6, 0));
        Assert.ThrowsExactly<InvalidOperationException>(() => UfwInsertionPlacementResolver.Resolve(malformed, FirewallAddressFamily.IPv4, 0));
    }

    [TestMethod]
    public void ResolveFamilyPosition_CountsOpaqueRowsAndAppendsPastSurvivingFamily()
    {
        ListedFirewallRule[] rules =
        [
            Parsed(FirewallAddressFamily.IPv4, "22"),
            Opaque("[ 2] unsupported ipv4 syntax"),
            Parsed(FirewallAddressFamily.IPv6, "80"),
            Opaque("[ 4] unsupported ipv6 syntax (v6)"),
        ];

        Assert.AreEqual(2, UfwInsertionPlacementResolver.GetFamilyPosition(rules, 1));
        Assert.AreEqual(2, UfwInsertionPlacementResolver.GetFamilyPosition(rules, 3));

        UfwInsertionPlacement existingIpv6Position = UfwInsertionPlacementResolver.ResolveFamilyPosition(rules, FirewallAddressFamily.IPv6, 2);
        UfwInsertionPlacement historicalIpv6Position = UfwInsertionPlacementResolver.ResolveFamilyPosition(rules, FirewallAddressFamily.IPv6, 5);

        Assert.AreEqual(3, existingIpv6Position.ExpectedOccurrenceIndex);
        Assert.AreEqual(4, existingIpv6Position.UfwInsertPosition);
        Assert.AreEqual(4, historicalIpv6Position.ExpectedOccurrenceIndex);
        Assert.IsNull(historicalIpv6Position.UfwInsertPosition);
    }

    [TestMethod]
    public void CreateCommand_UsesInsertOrAddFromResolvedPlacement()
    {
        ListedFirewallRule[] rules = [Parsed(FirewallAddressFamily.IPv4, "80")];
        FirewallRuleSpecification rule = Rule(FirewallAddressFamily.IPv4, "22");

        string[] insert = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv4, 0).CreateCommand(rule, s_renderer).BuildArguments().ToArray();
        string[] add = UfwInsertionPlacementResolver.Resolve(rules, FirewallAddressFamily.IPv4, 1).CreateCommand(rule, s_renderer).BuildArguments().ToArray();

        CollectionAssert.AreEqual(new[] { "insert", "1", "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" }, insert);
        CollectionAssert.AreEqual(new[] { "allow", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp" }, add);
    }

    private static ListedFirewallRule Parsed(FirewallAddressFamily family, string port) => new()
    {
        Parsed = true,
        RawLine = family == FirewallAddressFamily.IPv6 ? $"{port} (v6)" : port,
        Rule = Rule(family, port),
    };

    private static ListedFirewallRule Opaque(string rawLine) => new()
    {
        Parsed = false,
        RawLine = rawLine,
    };

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family, string port) => new()
    {
        AddressFamily = family,
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Source = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        Destination = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0",
        DestinationPorts = port,
        Protocol = FirewallProtocol.Tcp,
    };
}
