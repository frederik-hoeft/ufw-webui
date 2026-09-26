using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Features.Rules.Filtering.Actions;
using Ufw.Web.Client.Features.Rules.Filtering.Directions;
using Ufw.Web.Client.Features.Rules.Filtering.Groups;
using Ufw.Web.Client.Features.Rules.Filtering.KnownHosts;
using Ufw.Web.Client.Features.Rules.Filtering.Networks;
using Ufw.Web.Client.Features.Rules.Filtering.Ports;
using Ufw.Web.Client.Features.Rules.Filtering.Protocols;
using Ufw.Web.Client.Features.Rules.Filtering.Tags;
using Ufw.Web.Client.Features.Rules.Filtering.Text;
using Ufw.Web.Client.Features.Rules.Filtering;
using Ufw.Web.Client.Features.Rules.Metadata;
using Ufw.Web.Client.Features.Rules;

namespace Ufw.Web.Client.Tests.Features.Rules.Filtering;

[TestClass]
public sealed class RuleQueryServiceTests
{
    private readonly RuleQueryService _service = new(
    [
        new NetworkRuleFilterEvaluator(),
        new PortRuleFilterEvaluator(),
        new ProtocolRuleFilterEvaluator(),
        new ActionRuleFilterEvaluator(),
        new DirectionRuleFilterEvaluator(),
        new TagRuleFilterEvaluator(),
        new GroupRuleFilterEvaluator(),
        new TextRuleFilterEvaluator(new RuleKnownHostProjectionService()),
    ]);

    [TestMethod]
    public void Evaluate_ComposesFiltersConjunctivelyWithoutChangingCanonicalProjection()
    {
        RuleRowProjection first = Row(0, 1, source: "10.0.0.0/8", destinationPorts: "443", protocol: FirewallProtocol.Tcp, action: FirewallAction.Allow);
        RuleRowProjection second = Row(1, 2, source: "10.0.0.0/8", destinationPorts: "53", protocol: FirewallProtocol.Udp, action: FirewallAction.Allow);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [first, second]);
        Assert.IsTrue(RuleNetwork.TryParse("10.20.30.40", out RuleNetwork? network));
        Assert.IsTrue(RulePortSet.TryParse("443", out RulePortSet? ports));
        RuleQuery query = new([
            new NetworkRuleFilter(RuleEndpointField.Source, network!),
            new PortRuleFilter(RuleEndpointField.Destination, ports!),
            new ProtocolRuleFilter(FirewallProtocol.Tcp),
        ]);

        RuleFamilyQueryResult result = _service.Evaluate(family, query);

        Assert.AreEqual(2, result.TotalCount);
        Assert.HasCount(1, result.Rows);
        Assert.AreSame(first, result.Rows[0].Row);
        Assert.AreEqual(0, result.Rows[0].Row.OccurrenceId);
        Assert.AreEqual(1, result.Rows[0].Row.FamilyPosition);
        Assert.HasCount(3, result.Rows[0].Evidence);
    }

    [TestMethod]
    public void Evaluate_NetworkFilterUsesCidrIntersectionWithinSelectedFamily()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4,
        [
            Row(0, 1, source: "10.100.20.0/24"),
            Row(1, 2, source: "10.100.20.17"),
            Row(2, 3, source: "any"),
            Row(3, 4, source: "10.100.21.0/24"),
        ]);
        Assert.IsTrue(RuleNetwork.TryParse("10.100.20.0/28", out RuleNetwork? queryNetwork));
        RuleQuery query = new([new NetworkRuleFilter(RuleEndpointField.Source, queryNetwork!)]);

        RuleFamilyQueryResult result = _service.Evaluate(family, query);

        CollectionAssert.AreEqual(new[] { 0, 2 }, result.Rows.Select(static row => row.Row.OccurrenceId).ToArray());
        NetworkRuleMatchEvidence firstEvidence = (NetworkRuleMatchEvidence)result.Rows[0].Evidence.Single();
        Assert.AreEqual(NetworkRuleMatchEvidence.RelationshipKind.ContainsQuery, firstEvidence.Relationship);
        NetworkRuleMatchEvidence anyEvidence = (NetworkRuleMatchEvidence)result.Rows[1].Evidence.Single();
        Assert.AreEqual("0.0.0.0/0", anyEvidence.RuleNetwork);
    }

    [TestMethod]
    public void Evaluate_NetworkFilterCanMatchRuleContainedByQuery()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, destination: "192.0.2.40")]);
        Assert.IsTrue(RuleNetwork.TryParse("192.0.2.0/24", out RuleNetwork? queryNetwork));

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new NetworkRuleFilter(RuleEndpointField.Destination, queryNetwork!)]));

        Assert.HasCount(1, result.Rows);
        NetworkRuleMatchEvidence evidence = (NetworkRuleMatchEvidence)result.Rows[0].Evidence.Single();
        Assert.AreEqual(NetworkRuleMatchEvidence.RelationshipKind.ContainedByQuery, evidence.Relationship);
    }

    [TestMethod]
    public void Evaluate_NetworkFilterRejectsOtherAddressFamily()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, source: "any")]);
        Assert.IsTrue(RuleNetwork.TryParse("2001:db8::1", out RuleNetwork? queryNetwork));

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new NetworkRuleFilter(RuleEndpointField.Source, queryNetwork!)]));

        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Evaluate_IPv6NetworkFilterUsesFamilyLocalAnyNetwork()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv6,
        [
            Row(0, 1, addressFamily: FirewallAddressFamily.IPv6, source: "2001:db8:10::/48"),
            Row(1, 2, addressFamily: FirewallAddressFamily.IPv6, source: "any"),
            Row(2, 3, addressFamily: FirewallAddressFamily.IPv6, source: "2001:db8:20::/48"),
        ]);
        Assert.IsTrue(RuleNetwork.TryParse("2001:db8:10::1234", out RuleNetwork? queryNetwork));

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new NetworkRuleFilter(RuleEndpointField.Source, queryNetwork!)]));

        CollectionAssert.AreEqual(new[] { 0, 1 }, result.Rows.Select(static row => row.Row.OccurrenceId).ToArray());
        Assert.AreEqual("::/0", ((NetworkRuleMatchEvidence)result.Rows[1].Evidence.Single()).RuleNetwork);
    }

    [TestMethod]
    public void RuleNetwork_ParsesIPv4HostWithoutAccessingIPv6Scope()
    {
        Assert.IsTrue(RuleNetwork.TryParse("192.0.2.17", out RuleNetwork? network));
        Assert.IsNotNull(network);
        Assert.AreEqual(FirewallAddressFamily.IPv4, network.AddressFamily);
        Assert.AreEqual("192.0.2.17", network.CanonicalValue);
    }

    [TestMethod]
    public void RulePortSet_RejectsEmptyListSegments()
    {
        Assert.IsFalse(RulePortSet.TryParse("443,,8443", out _));
        Assert.IsFalse(RulePortSet.TryParse("443,", out _));
        Assert.IsFalse(RulePortSet.TryParse(",443", out _));
    }

    [TestMethod]
    public void Evaluate_PortFilterUsesRangeOverlapAndUnrestrictedEndpoints()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4,
        [
            Row(0, 1, destinationPorts: "1000:2000"),
            Row(1, 2, destinationPorts: "443"),
            Row(2, 3, destinationPorts: null),
        ]);
        Assert.IsTrue(RulePortSet.TryParse("1500,8443", out RulePortSet? ports));

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new PortRuleFilter(RuleEndpointField.Destination, ports!)]));

        CollectionAssert.AreEqual(new[] { 0, 2 }, result.Rows.Select(static row => row.Row.OccurrenceId).ToArray());
        Assert.AreEqual("any", ((PortRuleMatchEvidence)result.Rows[1].Evidence.Single()).RulePorts);
    }

    [TestMethod]
    public void Evaluate_TextFilterMatchesAcrossFieldsAndReturnsRanges()
    {
        RuleRowProjection row = Row(0, 1, source: "10.0.0.0/8", destinationPorts: "443", comment: "Prometheus metrics", protocol: FirewallProtocol.Tcp);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [row]);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("prometheus tcp")]));

        Assert.HasCount(1, result.Rows);
        Assert.HasCount(2, result.Rows[0].Evidence);
        TextRuleMatchEvidence comment = (TextRuleMatchEvidence)result.Rows[0].Evidence[0];
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.Comment, comment.Field);
        Assert.AreEqual(0, comment.Start);
        Assert.AreEqual(10, comment.Length);
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.Protocol, ((TextRuleMatchEvidence)result.Rows[0].Evidence[1]).Field);
    }

    [TestMethod]
    public void Evaluate_TagFilterMatchesEnrichedProjectionAndProducesEntityEvidence()
    {
        RuleTag observability = new(Guid.CreateVersion7(), "observability", "#336699");
        RuleMetadata metadata = new(Guid.CreateVersion7(), "Managed by platform", [observability, new RuleTag(Guid.CreateVersion7(), "prod", "#123456")]);
        RuleRowProjection row = Row(0, 1, metadata: metadata);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [row]);
        RuleTag filterTag = new(observability.Id, "renamed elsewhere", "#000000");
        RuleQuery query = new([new TagRuleFilter(filterTag)]);

        RuleFamilyQueryResult result = _service.Evaluate(family, query);

        Assert.HasCount(1, result.Rows);
        Assert.AreSame(row, result.Rows[0].Row);
        TagRuleMatchEvidence evidence = Assert.IsInstanceOfType<TagRuleMatchEvidence>(result.Rows[0].Evidence.Single());
        Assert.AreEqual(observability.Id, evidence.Tag.Id);
        Assert.AreEqual("observability", evidence.Tag.Name);
        Assert.AreEqual("#336699", evidence.Tag.Color);
    }

    [TestMethod]
    public void Evaluate_GroupFilterMatchesStableIdentityAndProducesCurrentMetadataEvidence()
    {
        Guid groupId = Guid.CreateVersion7();
        RuleGroupMembership membership = new(groupId, "operations", "Current group comment");
        RuleMetadata metadata = new(Guid.CreateVersion7(), null, [], membership);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, metadata: metadata)]);
        RuleGroup staleFilterPresentation = new(groupId, "old name", "old comment", []);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new GroupRuleFilter(staleFilterPresentation)]));

        Assert.HasCount(1, result.Rows);
        GroupRuleMatchEvidence evidence = Assert.IsInstanceOfType<GroupRuleMatchEvidence>(result.Rows[0].Evidence.Single());
        Assert.AreSame(membership, evidence.Group);
        Assert.AreEqual("operations", evidence.Group.Name);
    }

    [TestMethod]
    public void Evaluate_GroupFilterDoesNotUseDisplayNameAsIdentity()
    {
        RuleGroupMembership membership = new(Guid.CreateVersion7(), "ops", null);
        RuleMetadata metadata = new(Guid.CreateVersion7(), null, [], membership);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, metadata: metadata)]);
        RuleGroup differentGroup = new(Guid.CreateVersion7(), "ops", null, []);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new GroupRuleFilter(differentGroup)]));

        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Evaluate_TagFilterDoesNotUseDisplayNameAsIdentity()
    {
        RuleTag attachedTag = new(Guid.CreateVersion7(), "prod", "#336699");
        RuleMetadata metadata = new(Guid.CreateVersion7(), null, [attachedTag]);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, metadata: metadata)]);
        RuleTag differentTag = new(Guid.CreateVersion7(), "prod", "#336699");

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TagRuleFilter(differentTag)]));

        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Evaluate_TextFilterSearchesMetadataAndReturnsFieldSpecificEvidence()
    {
        RuleMetadata metadata = new(Guid.CreateVersion7(), "Owned by platform team", [new RuleTag(Guid.CreateVersion7(), "observability", "#336699")]);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, metadata: metadata)]);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("platform observability")]));

        Assert.HasCount(1, result.Rows);
        Assert.HasCount(2, result.Rows[0].Evidence);
        TextRuleMatchEvidence notes = (TextRuleMatchEvidence)result.Rows[0].Evidence[0];
        TextRuleMatchEvidence tag = (TextRuleMatchEvidence)result.Rows[0].Evidence[1];
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.Notes, notes.Field);
        Assert.AreEqual("platform", notes.Term);
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.Tag, tag.Field);
        Assert.AreEqual("observability", tag.Value);
    }

    [TestMethod]
    public void Evaluate_TextFilterSearchesGroupNameAndCommentWithFieldSpecificEvidence()
    {
        RuleMetadata metadata = new(Guid.CreateVersion7(), null, [], new RuleGroupMembership(Guid.CreateVersion7(), "remote-admin", "Trusted SSH access"));
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, metadata: metadata)]);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("remote-admin trusted")]));

        Assert.HasCount(1, result.Rows);
        Assert.HasCount(2, result.Rows[0].Evidence);
        TextRuleMatchEvidence groupName = Assert.IsInstanceOfType<TextRuleMatchEvidence>(result.Rows[0].Evidence[0]);
        TextRuleMatchEvidence groupComment = Assert.IsInstanceOfType<TextRuleMatchEvidence>(result.Rows[0].Evidence[1]);
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.GroupName, groupName.Field);
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.GroupComment, groupComment.Field);
        Assert.AreEqual("Trusted SSH access", groupComment.Value);
    }

    [TestMethod]
    public void Evaluate_TextFilterMatchesKnownHostProjectedThroughContainingEndpointNetwork()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4,
        [
            Row(0, 1, source: "10.100.20.0/24", destination: "192.0.2.1"),
            Row(1, 2, source: "10.100.30.0/24", destination: "192.0.2.2"),
        ]);
        KnownHostInventoryItem host = new()
        {
            Id = Guid.CreateVersion7(),
            Name = "nas1.service.home.arpa",
            Address = "10.100.20.17",
            AddressFamily = FirewallAddressFamily.IPv4,
            IsVisible = true,
        };

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("nas1")]), [host]);

        Assert.HasCount(1, result.Rows);
        Assert.AreEqual(0, result.Rows[0].Row.OccurrenceId);
        TextRuleMatchEvidence evidence = Assert.IsInstanceOfType<TextRuleMatchEvidence>(result.Rows[0].Evidence.Single());
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.SourceKnownHost, evidence.Field);
        StringAssert.Contains(evidence.Value, "nas1.service.home.arpa");
    }

    [TestMethod]
    public void Evaluate_TextFilterKnownHostProjectionRespectsVisibilityAndAddressFamily()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4,
        [
            Row(0, 1, source: "10.100.20.0/24", destination: "192.0.2.1"),
            Row(1, 2, source: "10.100.30.0/24", destination: "192.0.2.2"),
        ]);
        KnownHostInventoryItem[] hosts =
        [
            KnownHost("nas-visible", "10.100.20.17", FirewallAddressFamily.IPv4, isVisible: true),
            KnownHost("nas-hidden", "10.100.30.17", FirewallAddressFamily.IPv4, isVisible: false),
            KnownHost("nas-v6", "2001:db8::17", FirewallAddressFamily.IPv6, isVisible: true),
        ];

        RuleFamilyQueryResult visible = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("visible")]), hosts);
        RuleFamilyQueryResult hidden = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("hidden")]), hosts);
        RuleFamilyQueryResult otherFamily = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("nas-v6")]), hosts);

        CollectionAssert.AreEqual(new[] { 0 }, visible.Rows.Select(static row => row.Row.OccurrenceId).ToArray());
        Assert.IsEmpty(hidden.Rows);
        Assert.IsEmpty(otherFamily.Rows);
    }

    [TestMethod]
    public void Evaluate_TextFilterKnownHostProjectionTreatsAnyAsContainingAndReportsDestination()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4,
        [
            Row(0, 1, source: "192.0.2.1", destination: "any"),
        ]);
        KnownHostInventoryItem host = KnownHost("db1.service.home.arpa", "10.100.20.17", FirewallAddressFamily.IPv4, isVisible: true, comment: "primary database");

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("database")]), [host]);

        Assert.HasCount(1, result.Rows);
        TextRuleMatchEvidence evidence = Assert.IsInstanceOfType<TextRuleMatchEvidence>(result.Rows[0].Evidence.Single());
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.DestinationKnownHost, evidence.Field);
        StringAssert.Contains(evidence.Value, "db1.service.home.arpa [10.100.20.17] primary database");
    }

    [TestMethod]
    public void Evaluate_TextFilterKnownHostProjectionMatchesRuleContainedByKnownNetworkAlias()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, source: "10.100.20.17", destination: "192.0.2.1")]);
        KnownHostInventoryItem host = KnownHost("storage-net", "10.100.20.0/24", FirewallAddressFamily.IPv4, isVisible: true);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("storage")]), [host]);

        Assert.HasCount(1, result.Rows);
        TextRuleMatchEvidence evidence = Assert.IsInstanceOfType<TextRuleMatchEvidence>(result.Rows[0].Evidence.Single());
        Assert.AreEqual(TextRuleMatchEvidence.FieldKind.SourceKnownHost, evidence.Field);
    }

    [TestMethod]
    public void Evaluate_TextFilterKnownHostProjectionDoesNotMatchHostOutsideRuleNetwork()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [Row(0, 1, source: "10.100.20.0/24", destination: "192.0.2.1")]);
        KnownHostInventoryItem host = KnownHost("nas1.service.home.arpa", "10.100.21.17", FirewallAddressFamily.IPv4, isVisible: true);

        RuleFamilyQueryResult result = _service.Evaluate(family, new RuleQuery([new TextRuleFilter("nas1")]), [host]);

        Assert.IsEmpty(result.Rows);
    }

    [TestMethod]
    public void Evaluate_TextSearchCanMatchOpaqueRawRuleButStructuredFilterCannot()
    {
        ListedFirewallRule opaque = new() { Parsed = false, RawLine = "custom opaque rule for monitoring" };
        RuleRowProjection row = new(opaque, FirewallAddressFamily.IPv4, 0, 1, 1, false, false, null);
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, [row]);

        Assert.HasCount(1, _service.Evaluate(family, new RuleQuery([new TextRuleFilter("monitoring")])).Rows);
        RuleQuery structured = new([new ActionRuleFilter(FirewallAction.Allow)]);
        Assert.IsEmpty(_service.Evaluate(family, structured).Rows);
    }

    [TestMethod]
    public void Evaluate_RejectsUnregisteredFilterEvenWhenFamilyHasNoRows()
    {
        RuleFamilyProjection family = new(FirewallAddressFamily.IPv4, []);
        RuleQuery query = new([new UnregisteredRuleFilter()]);

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => _service.Evaluate(family, query));

        StringAssert.Contains(exception.Message, nameof(UnregisteredRuleFilter));
    }

    [TestMethod]
    public void TextRuleFilter_ParsesQuotedFreeTextAsOneTerm()
    {
        TextRuleFilter filter = new("prometheus \"home network\" tcp");

        CollectionAssert.AreEqual(new[] { "prometheus", "home network", "tcp" }, filter.Terms.ToArray());
    }

    private sealed record UnregisteredRuleFilter : RuleFilter;

    private static KnownHostInventoryItem KnownHost(
        string name,
        string address,
        FirewallAddressFamily addressFamily,
        bool isVisible,
        string? comment = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Address = address,
        AddressFamily = addressFamily,
        IsVisible = isVisible,
        Comment = comment,
    };

    private static RuleRowProjection Row(
        int occurrenceId,
        int familyPosition,
        FirewallAddressFamily addressFamily = FirewallAddressFamily.IPv4,
        string? source = "any",
        string? destination = "any",
        string? sourcePorts = null,
        string? destinationPorts = null,
        FirewallProtocol protocol = FirewallProtocol.Any,
        FirewallAction action = FirewallAction.Allow,
        FirewallDirection direction = FirewallDirection.In,
        string? comment = null,
        RuleMetadata? metadata = null)
    {
        FirewallRuleSpecification specification = new()
        {
            AddressFamily = addressFamily,
            Source = source,
            Destination = destination,
            SourcePorts = sourcePorts,
            DestinationPorts = destinationPorts,
            Protocol = protocol,
            Action = action,
            Direction = direction,
            Comment = comment,
        };
        ListedFirewallRule rule = new() { Parsed = true, RuleId = $"rule-{occurrenceId}", RawLine = $"raw rule {occurrenceId}", Rule = specification };
        return new RuleRowProjection(rule, addressFamily, occurrenceId, familyPosition, 4, true, true, null, metadata);
    }
}
