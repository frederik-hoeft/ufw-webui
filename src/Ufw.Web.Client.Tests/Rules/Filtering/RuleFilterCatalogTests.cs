using Ufw.Shared.Firewall;
using Ufw.Web.Client.Components.Rules.Filtering.Actions;
using Ufw.Web.Client.Components.Rules.Filtering.Directions;
using Ufw.Web.Client.Components.Rules.Filtering.Networks;
using Ufw.Web.Client.Components.Rules.Filtering.Ports;
using Ufw.Web.Client.Components.Rules.Filtering.Protocols;
using Ufw.Web.Client.Components.Rules.Filtering.Tags;
using Ufw.Web.Client.Components.Rules.Filtering.Text;
using Ufw.Web.Client.Components.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering.Directions;
using Ufw.Web.Client.Features.Rules.Filtering.Networks;
using Ufw.Web.Client.Features.Rules.Filtering.Ports;
using Ufw.Web.Client.Features.Rules.Filtering.Protocols;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Filtering.Text;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Metadata;

namespace Ufw.Web.Client.Tests.Rules.Filtering;

[TestClass]
public sealed class RuleFilterCatalogTests
{
    private readonly RuleFilterCatalog _catalog = CreateCatalog();

    [TestMethod]
    public void Definitions_HaveUniqueKeysAndCompatibleEditors()
    {
        IReadOnlyList<RuleFilterDefinition> definitions = _catalog.Definitions;

        Assert.AreEqual(definitions.Count, definitions.Select(static definition => definition.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(definitions.All(static definition => typeof(IRuleFilterEditor).IsAssignableFrom(definition.EditorComponentType)));
        Assert.HasCount(7, definitions.Where(static definition => definition.Selectable));
        Assert.IsFalse(definitions.Single(static definition => definition.Key == "text").Selectable);
    }

    [TestMethod]
    public void Definitions_DistinguishSourceAndDestinationNetworkEditors()
    {
        RuleFilterDefinition source = _catalog.Definitions.Single(static definition => definition.Key == "source-network");
        RuleFilterDefinition destination = _catalog.Definitions.Single(static definition => definition.Key == "destination-network");

        Assert.AreEqual(typeof(NetworkRuleFilterEditor), source.EditorComponentType);
        Assert.AreEqual(typeof(NetworkRuleFilterEditor), destination.EditorComponentType);
        Assert.AreEqual(true, source.Parameters[nameof(NetworkRuleFilterEditor.SourceEndpoint)]);
        Assert.AreEqual(false, destination.Parameters[nameof(NetworkRuleFilterEditor.SourceEndpoint)]);
    }

    [TestMethod]
    public void Resolve_MapsEveryConfiguredPhaseTwoFilterToExactlyOneDefinition()
    {
        Assert.IsTrue(RuleNetwork.TryParse("10.0.0.0/8", out RuleNetwork? network));
        Assert.IsTrue(RulePortSet.TryParse("443", out RulePortSet? ports));
        RuleFilter[] filters =
        [
            new TextRuleFilter("ssh"),
            new NetworkRuleFilter(RuleEndpointField.Source, network!),
            new NetworkRuleFilter(RuleEndpointField.Destination, network!),
            new PortRuleFilter(RuleEndpointField.Any, ports!),
            new ProtocolRuleFilter(FirewallProtocol.Tcp),
            new ActionRuleFilter(FirewallAction.Allow),
            new DirectionRuleFilter(FirewallDirection.In),
            new TagRuleFilter(new RuleTag(Guid.CreateVersion7(), "prod", "#336699")),
        ];

        string[] keys = filters.Select(filter => _catalog.Resolve(filter).Key).ToArray();

        CollectionAssert.AreEqual(new[] { "text", "source-network", "destination-network", "port", "protocol", "action", "direction", "tag" }, keys);
    }

    [TestMethod]
    public void Resolve_RejectsFilterWithoutUiRegistration()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => _catalog.Resolve(new UnregisteredRuleFilter()));

        StringAssert.Contains(exception.Message, nameof(UnregisteredRuleFilter));
    }

    [TestMethod]
    public void Constructor_RejectsDuplicateStableKeys()
    {
        RuleFilterDefinition duplicate = new(
            "duplicate",
            "SearchRules",
            "FilterCategoryGeneral",
            typeof(TextRuleFilterEditor),
            static filter => filter is TextRuleFilter,
            static (_, _, _) => "text");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => new RuleFilterCatalog(
        [
            new TestDefinitionProvider(duplicate),
            new TestDefinitionProvider(duplicate),
        ]));

        StringAssert.Contains(exception.Message, "duplicate");
    }

    private static RuleFilterCatalog CreateCatalog() => new(
    [
        new NetworkRuleFilterDefinitionProvider(),
        new PortRuleFilterDefinitionProvider(),
        new ProtocolRuleFilterDefinitionProvider(),
        new ActionRuleFilterDefinitionProvider(),
        new DirectionRuleFilterDefinitionProvider(),
        new TagRuleFilterDefinitionProvider(),
        new TextRuleFilterDefinitionProvider(),
    ]);

    private sealed record UnregisteredRuleFilter : RuleFilter;

    private sealed class TestDefinitionProvider(params RuleFilterDefinition[] definitions) : IRuleFilterDefinitionProvider
    {
        public IReadOnlyList<RuleFilterDefinition> Definitions { get; } = definitions;
    }
}
