using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Interop.Commands;

namespace Ufw.Systemd.Tests.Interop.Commands;

[TestClass]
public sealed class UfwRuleCommandTests
{
    private static readonly IUfwRuleCommandRenderer s_renderer = new UfwRuleCommandRenderer();
    private static readonly string[] s_expectedDeleteArguments = ["--force", "delete", "12"];

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
    public void DeleteRule_BuildArguments_RejectsNonPositiveDisplayNumber()
    {
        UfwDeleteRuleCommand command = new(0);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => command.BuildArguments());
    }
}
