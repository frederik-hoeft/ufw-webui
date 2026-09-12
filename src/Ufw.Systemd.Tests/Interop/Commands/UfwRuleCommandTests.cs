using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Tests.Interop.Commands;

[TestClass]
public sealed class UfwRuleCommandTests
{
    private static readonly IUfwRuleCommandRenderer s_renderer = new UfwRuleCommandRenderer();
    private static readonly string[] s_expectedDeleteArguments = ["--force", "delete", "12"];
    private static readonly string[] s_expectedInsertArguments =
    [
        "insert", "3", "deny", "in", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "22", "proto", "tcp",
    ];
    private static readonly string[] s_expectedRouteInsertArguments =
    [
        "route", "insert", "4", "allow", "in", "on", "eth0", "out", "on", "eth1",
        "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "443", "proto", "tcp",
    ];

    [TestMethod]
    public void AddRule_BuildArguments_UsesSharedCanonicalRuleTokens()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
            DestinationInterface = "eth0",
            Comment = "ssh",
        };
        UfwRenderedRule rendered = s_renderer.Render(rule);
        UfwAddRuleCommand command = new(rule, s_renderer);

        CollectionAssert.AreEqual(rendered.Arguments.ToArray(), command.BuildArguments().ToArray());
    }

    [TestMethod]
    public void DeleteRule_BuildArguments_FormatsDecimalDisplayNumber()
    {
        UfwDeleteRuleCommand command = new(12);

        CollectionAssert.AreEqual(s_expectedDeleteArguments, command.BuildArguments().ToArray());
    }

    [TestMethod]
    public void InsertRule_BuildArguments_PlacesPositionBeforeOrdinaryRule()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Deny,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
        };
        UfwInsertRuleCommand command = new(3, rule, s_renderer);

        CollectionAssert.AreEqual(s_expectedInsertArguments, command.BuildArguments().ToArray());
    }

    [TestMethod]
    public void InsertRule_BuildArguments_PreservesRouteCommandFamily()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.Forward,
            Protocol = FirewallProtocol.Tcp,
            SourceInterface = "eth0",
            DestinationInterface = "eth1",
            DestinationPorts = "443",
        };
        UfwInsertRuleCommand command = new(4, rule, s_renderer);

        CollectionAssert.AreEqual(s_expectedRouteInsertArguments, command.BuildArguments().ToArray());
    }

    [TestMethod]
    public void InsertRule_BuildArguments_RejectsNonPositiveDisplayNumber()
    {
        UfwInsertRuleCommand command = new(0, new FirewallRuleSpecification(), s_renderer);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => command.BuildArguments());
    }

    [TestMethod]
    public void DeleteRule_BuildArguments_RejectsNonPositiveDisplayNumber()
    {
        UfwDeleteRuleCommand command = new(0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => command.BuildArguments());
    }
}
