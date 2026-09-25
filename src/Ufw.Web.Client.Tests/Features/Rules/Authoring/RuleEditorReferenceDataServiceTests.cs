using Moq;
using Ufw.Shared.Firewall;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Client.Features.NetworkInterfaces;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Services.Errors;

namespace Ufw.Web.Client.Tests.Features.Rules.Authoring;

[TestClass]
public sealed class RuleEditorReferenceDataServiceTests
{
    [TestMethod]
    public async Task LoadAsync_PreservesInventoryAndCentralizesVisibilityPolicyAsync()
    {
        KnownHostInventoryItem visibleV4 = Host("v4", FirewallAddressFamily.IPv4, isVisible: true);
        KnownHostInventoryItem visibleV6 = Host("v6", FirewallAddressFamily.IPv6, isVisible: true);
        KnownHostInventoryItem hidden = Host("hidden", FirewallAddressFamily.IPv4, isVisible: false);
        NetworkInterfaceInventoryItem visibleInterface = NetworkInterface("eno1", isVisible: true);
        NetworkInterfaceInventoryItem hiddenInterface = NetworkInterface("wg0", isVisible: false);
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new(MockBehavior.Strict);
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse { Hosts = [visibleV4, visibleV6, hidden] });
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetworkInterfaceInventoryResponse { Interfaces = [visibleInterface, hiddenInterface] });
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        CollectionAssert.AreEqual(new[] { visibleInterface }, data.VisibleInterfaces.ToArray());
        CollectionAssert.AreEqual(new[] { visibleV4 }, service.GetVisibleKnownHosts(data, ipv6Enabled: false).ToArray());
        CollectionAssert.AreEqual(new[] { visibleV4, visibleV6 }, service.GetVisibleKnownHosts(data, ipv6Enabled: true).ToArray());
        Assert.IsTrue(service.IsUnknownInterface(data, "missing0"));
        Assert.IsFalse(service.IsUnknownInterface(data, "wg0"));
    }

    [TestMethod]
    public async Task LoadAsync_KnownHostFailureDoesNotDiscardInterfaceSuggestionsAsync()
    {
        HttpRequestException failure = new("known hosts unavailable");
        NetworkInterfaceInventoryItem networkInterface = NetworkInterface("eno1", isVisible: true);
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new();
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventoryResponse { Interfaces = [networkInterface] });
        ClientError clientError = new(ClientErrorKind.Unavailable, "known hosts unavailable", true);
        errors.Setup(mapper => mapper.TryDescribe(failure, out It.Ref<ClientError>.IsAny)).Returns((Exception _, out ClientError error) =>
        {
            error = clientError;
            return true;
        });
        errors.Setup(mapper => mapper.Describe(failure)).Returns(clientError);
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        Assert.IsEmpty(data.KnownHosts);
        CollectionAssert.AreEqual(new[] { networkInterface }, data.VisibleInterfaces.ToArray());
        errors.Verify(mapper => mapper.Describe(failure), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_InterfaceFailureSuppressesUnknownInterfaceWarningsAsync()
    {
        HttpRequestException failure = new("interfaces unavailable");
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new();
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse());
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        errors.Setup(mapper => mapper.Describe(failure)).Returns(new ClientError(ClientErrorKind.Unavailable, "interface lookup failed", true));
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        Assert.AreEqual("interface lookup failed", data.InterfaceInventoryError);
        Assert.IsEmpty(data.KnownInterfaces);
        Assert.IsFalse(service.IsUnknownInterface(data, "eno1"));
    }

    private static KnownHostInventoryItem Host(string name, FirewallAddressFamily family, bool isVisible) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Address = family == FirewallAddressFamily.IPv6 ? "2001:db8::1" : "192.0.2.1",
        AddressFamily = family,
        IsVisible = isVisible,
    };

    private static NetworkInterfaceInventoryItem NetworkInterface(string name, bool isVisible) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        IsVisible = isVisible,
    };
}
