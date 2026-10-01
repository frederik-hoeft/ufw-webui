using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules.Templates;

namespace Ufw.Web.Client.Tests.Features.Rules.Templates;

[TestClass]
public sealed class RuleTemplateAuthoringServiceTests
{
    private readonly RuleTemplateAuthoringService _service = new(new RuleDraftFactory());

    [TestMethod]
    public void CreateDefinition_CopiesNormalizedRuleAndFullMetadataWithoutLiveIdentityState()
    {
        Guid tagA = Guid.CreateVersion7();
        Guid tagB = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        FirewallRuleSpecification source = Rule(FirewallAddressFamily.IPv6);
        source.Source = "2001:0db8::1";
        RuleMetadata metadata = new(
            Guid.CreateVersion7(),
            "  keep this  ",
            [new RuleTag(tagB, "beta", "#223344"), new RuleTag(tagA, "alpha", "#112233"), new RuleTag(tagA, "alpha", "#112233")],
            new RuleGroupMembership(groupId, "ops", null));

        RuleTemplateDefinition definition = _service.CreateDefinition("  web  ", "  inbound HTTPS  ", source, metadata);

        Assert.AreEqual("web", definition.Name);
        Assert.AreEqual("inbound HTTPS", definition.Description);
        Assert.AreEqual("2001:db8::1", definition.Rule.Source);
        Assert.AreEqual("keep this", definition.Notes);
        CollectionAssert.AreEqual(new[] { tagA, tagB }.Order().ToArray(), definition.TagIds.ToArray());
        Assert.AreEqual(groupId, definition.GroupId);
        Assert.AreNotSame(source, definition.Rule);
        Assert.AreEqual("2001:0db8::1", source.Source);
    }

    [TestMethod]
    public void Initialize_AppendCreationClonesAllTemplateValuesIndependently()
    {
        Guid tagId = Guid.CreateVersion7();
        Guid groupId = Guid.CreateVersion7();
        RuleTemplate template = Template(Rule(FirewallAddressFamily.IPv6), "notes", [new RuleTag(tagId, "prod", "#112233")], new RuleGroupMembership(groupId, "edge", null));

        RuleTemplateInstantiationResult result = _service.Initialize(template);

        Assert.IsTrue(result.Succeeded);
        RuleTemplateInstantiation instantiation = result.Instantiation!;
        Assert.AreEqual(FirewallAddressFamily.IPv6, instantiation.Rule.AddressFamily);
        Assert.AreEqual("notes", instantiation.Notes);
        Assert.AreEqual(tagId, instantiation.TagIds.Single());
        Assert.AreEqual(groupId, instantiation.GroupId);
        Assert.AreNotSame(template.Rule, instantiation.Rule);
        instantiation.Rule.Source = "203.0.113.10";
        Assert.AreEqual("10.0.0.0/8", template.Rule.Source);
    }

    [TestMethod]
    public void Initialize_OrderedInsertionSpecializesFamilyNeutralTemplateToRequiredFamily()
    {
        FirewallRuleSpecification familyNeutral = Rule(FirewallAddressFamily.Any);
        familyNeutral.Source = "anywhere";
        familyNeutral.Destination = "anywhere";
        RuleTemplate template = Template(familyNeutral);

        RuleTemplateInstantiationResult result = _service.Initialize(template, FirewallAddressFamily.IPv6);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(FirewallAddressFamily.IPv6, result.Instantiation!.Rule.AddressFamily);
        Assert.AreEqual(FirewallAddressFamily.Any, template.Rule.AddressFamily);
    }

    [TestMethod]
    public void Initialize_OrderedInsertionAcceptsMatchingConcreteFamily()
    {
        RuleTemplate template = Template(Rule(FirewallAddressFamily.IPv4));

        RuleTemplateInstantiationResult result = _service.Initialize(template, FirewallAddressFamily.IPv4);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(FirewallAddressFamily.IPv4, result.Instantiation!.Rule.AddressFamily);
    }

    [TestMethod]
    public void Initialize_OrderedInsertionRejectsConflictingConcreteFamilyWithoutProducingDraft()
    {
        RuleTemplate template = Template(Rule(FirewallAddressFamily.IPv6));

        RuleTemplateInstantiationResult result = _service.Initialize(template, FirewallAddressFamily.IPv4);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(RuleTemplateInstantiationError.AddressFamilyMismatch, result.Error);
        Assert.IsNull(result.Instantiation);
        Assert.AreEqual(FirewallAddressFamily.IPv6, template.Rule.AddressFamily);
    }

    private static RuleTemplate Template(
        FirewallRuleSpecification rule,
        string? notes = null,
        IReadOnlyList<RuleTag>? tags = null,
        RuleGroupMembership? group = null) => new(Guid.CreateVersion7(), "web", null, rule, notes, tags ?? [], group);

    private static FirewallRuleSpecification Rule(FirewallAddressFamily family) => new()
    {
        AddressFamily = family,
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        Source = "10.0.0.0/8",
        Destination = "192.0.2.10",
        DestinationPorts = "443",
    };
}
