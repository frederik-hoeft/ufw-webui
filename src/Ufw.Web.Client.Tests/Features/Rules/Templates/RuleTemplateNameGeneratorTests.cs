using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Web.Client.Features.Rules.Templates;
using Ufw.Web.Model.V1.RuleTemplates;

namespace Ufw.Web.Client.Tests.Features.Rules.Templates;

[TestClass]
public sealed class RuleTemplateNameGeneratorTests
{
    private readonly RuleTemplateNameGenerator _generator = new(new UfwRuleCommandRenderer());

    [TestMethod]
    public void Generate_NormalizedRuleSummaryOmitsComment()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.In,
            AddressFamily = FirewallAddressFamily.IPv4,
            Protocol = FirewallProtocol.Tcp,
            Source = "192.0.2.0/24",
            Destination = "10.1.0.5",
            DestinationPorts = "443",
            Comment = "Do not put this in the display name",
        };

        string name = _generator.Generate(rule);

        Assert.AreEqual("allow in from 192.0.2.0/24 to 10.1.0.5 port 443 proto tcp", name);
        Assert.IsFalse(name.Contains("comment", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Generate_LongValidRuleHasBoundedDisplayName()
    {
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            Direction = FirewallDirection.Forward,
            AddressFamily = FirewallAddressFamily.IPv4,
            Protocol = FirewallProtocol.Tcp,
            Source = "192.0.2.0/24",
            Destination = "10.1.0.0/16",
            SourcePorts = string.Join(',', Enumerable.Range(0, 30).Select(static offset => 1000 + offset * 2)),
            DestinationPorts = string.Join(',', Enumerable.Range(0, 30).Select(static offset => 2000 + offset * 2)),
        };

        string name = _generator.Generate(rule);

        Assert.IsTrue(name.Length <= RuleTemplateLimits.MAX_NAME_LENGTH);
        Assert.IsTrue(name.EndsWith("...", StringComparison.Ordinal));
    }
}
