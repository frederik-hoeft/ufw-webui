using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Templates;

namespace Ufw.Web.Client.Tests.Features.Rules.Templates;

[TestClass]
public sealed class RuleTemplateDraftFactoryTests
{
    private readonly RuleTemplateDraftFactory _factory = new(new RuleDraftFactory());

    [TestMethod]
    public void CreateFromExisting_ProducesIndependentNormalizedDraft()
    {
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        FirewallRuleSpecification rule = new()
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv6,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
            Source = "2001:0db8::1",
            Destination = "anywhere",
            DestinationPorts = "443",
        };
        RuleTemplate source = new(Guid.CreateVersion7(), "web", "desc", rule, "notes", [new RuleTag(tagId, "prod", "#112233")], new RuleGroupMembership(groupId, "edge", null));

        RuleTemplateDraft draft = _factory.CreateFromExisting(source);
        Assert.AreEqual("2001:db8::1", draft.Rule.Source);
        draft.Rule.Source = "2001:db8::2";
        draft.TagIds = [];
        draft.Name = "changed";

        Assert.AreEqual("2001:0db8::1", source.Rule.Source);
        Assert.AreEqual("web", source.Name);
        Assert.AreEqual(tagId, source.TagIds.Single());
        Assert.AreEqual("changed", draft.Name);
        Assert.IsEmpty(draft.TagIds);
        Assert.AreEqual(groupId, draft.GroupId);
    }

    [TestMethod]
    public void Create_UsesCapabilityNeutralFamilyDefault()
    {
        RuleTemplateDraft draft = _factory.Create();

        Assert.AreEqual(FirewallAddressFamily.Any, draft.Rule.AddressFamily);
        Assert.AreEqual(string.Empty, draft.Name);
    }
}
