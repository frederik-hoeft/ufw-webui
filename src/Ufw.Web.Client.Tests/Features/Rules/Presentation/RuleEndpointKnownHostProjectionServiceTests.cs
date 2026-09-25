using Ufw.Shared.Firewall;
using Ufw.Web.Client.Features.Rules.Presentation;
using Ufw.Web.Model.V1.KnownHosts;

namespace Ufw.Web.Client.Tests.Features.Rules.Presentation;

[TestClass]
public sealed class RuleEndpointKnownHostProjectionServiceTests
{
    private readonly RuleEndpointKnownHostProjectionService _service = new();

    [TestMethod]
    public void Project_AnyOrInvalidEndpointIsNotMeaningful()
    {
        Assert.IsFalse(_service.Project("any", []).IsMeaningful);
        Assert.IsFalse(_service.Project(null, []).IsMeaningful);
        Assert.IsFalse(_service.Project("not-an-address", []).IsMeaningful);
    }

    [TestMethod]
    public void Project_NormalizesEndpointAndMatchesOnlyExactKnownHostAddresses()
    {
        KnownHostInventoryItem exact = Host("network", "10.20.0.0/24", FirewallAddressFamily.IPv4);
        KnownHostInventoryItem contained = Host("host", "10.20.0.5", FirewallAddressFamily.IPv4);

        RuleEndpointKnownHostProjection projection = _service.Project("10.20.0.17/24", [exact, contained]);

        Assert.AreEqual("10.20.0.0/24", projection.Address);
        CollectionAssert.AreEqual(new[] { exact }, projection.Hosts.ToArray());
    }

    [TestMethod]
    public void Project_ReturnsEveryAliasForTheExactAddressRegardlessOfSuggestionVisibility()
    {
        KnownHostInventoryItem visible = Host("primary", "192.0.2.10", FirewallAddressFamily.IPv4, isVisible: true);
        KnownHostInventoryItem hidden = Host("legacy", "192.0.2.10", FirewallAddressFamily.IPv4, isVisible: false);
        KnownHostInventoryItem ipv6 = Host("v6", "2001:db8::10", FirewallAddressFamily.IPv6);

        RuleEndpointKnownHostProjection projection = _service.Project("192.0.2.10", [visible, hidden, ipv6]);

        CollectionAssert.AreEqual(new[] { visible, hidden }, projection.Hosts.ToArray());
    }

    private static KnownHostInventoryItem Host(string name, string address, FirewallAddressFamily family, bool isVisible = true) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Address = address,
        AddressFamily = family,
        IsVisible = isVisible,
    };
}
