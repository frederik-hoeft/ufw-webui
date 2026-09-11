using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;

namespace Ufw.Shared.Tests.Firewall.Rendering;

[TestClass]
public sealed class UfwRuleCommandRendererTests
{
    private readonly IUfwRuleCommandRenderer _renderer = new UfwRuleCommandRenderer();

    [TestMethod]
    public void Render_UsesCanonicalLongFormTokens()
    {
        UfwRenderedRule rendered = _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            DestinationPorts = "22",
            DestinationInterface = "eth0",
            Comment = "ssh",
        });

        string[] expected =
        [
            "allow", "in", "on", "eth0", "from", "any", "to", "any", "port", "22", "proto", "tcp", "comment", "ssh"
        ];
        CollectionAssert.AreEqual(expected, rendered.Arguments.ToArray());
        Assert.AreEqual("allow in on eth0 from any to any port 22 proto tcp comment ssh", rendered.DisplayText);
    }

    [TestMethod]
    public void Render_RouteUsesInOnAndOutOn()
    {
        UfwRenderedRule rendered = _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.Forward,
            Protocol = FirewallProtocol.Any,
            Source = "10.0.0.0/8",
            SourceInterface = "br0",
            Destination = "192.168.0.0/16",
            DestinationInterface = "eth0",
        });

        string[] expected =
        [
            "route", "allow", "in", "on", "br0", "out", "on", "eth0", "from", "10.0.0.0/8", "to", "192.168.0.0/16"
        ];
        CollectionAssert.AreEqual(expected, rendered.Arguments.ToArray());
        Assert.AreEqual("route allow in on br0 out on eth0 from 10.0.0.0/8 to 192.168.0.0/16", rendered.DisplayText);
    }

    [TestMethod]
    public void Render_UsesAddressFamilySpecificAnywhereForConcreteFamilies()
    {
        UfwRenderedRule ipv4 = _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            DestinationPorts = "22",
        });
        CollectionAssert.Contains(ipv4.Arguments.ToArray(), "0.0.0.0/0");

        UfwRenderedRule ipv6 = _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv6,
            Direction = FirewallDirection.In,
            DestinationPorts = "22",
        });
        CollectionAssert.Contains(ipv6.Arguments.ToArray(), "::/0");
    }

    [TestMethod]
    public void Render_QuotesWhitespaceInHumanReadableTextWithoutChangingArguments()
    {
        UfwRenderedRule rendered = _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Comment = "SSH access",
        });

        Assert.AreEqual("SSH access", rendered.Arguments[^1]);
        Assert.AreEqual("allow in from any to any comment \"SSH access\"", rendered.DisplayText);
    }

    [TestMethod]
    public void Render_RejectsAmbiguousNonForwardInterfaces()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            SourceInterface = "eth1",
            DestinationInterface = "eth0",
        }));

        Assert.ThrowsExactly<InvalidOperationException>(() => _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.Out,
            DestinationInterface = "eth1",
        }));
    }

    [TestMethod]
    public void Render_RejectsUnsafeComment()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            Comment = "ok; rm -rf /",
        }));
    }

    [TestMethod]
    public void Render_RejectsNewlinesInInterface()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _renderer.Render(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            DestinationInterface = "eth0\n--dry-run",
        }));
    }
    [TestMethod]
    public void TryRender_InvalidRule_ReturnsFalseWithoutPresentation()
    {
        bool rendered = _renderer.TryRender(new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            DestinationInterface = "eth0\n--dry-run",
        }, out UfwRenderedRule? result);

        Assert.IsFalse(rendered);
        Assert.IsNull(result);
    }
}
