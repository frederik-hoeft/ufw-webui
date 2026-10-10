using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Management.KnownHosts;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Client.Api.KnownHosts;
using Ufw.Web.Client.Api.NetworkInterfaces;
using Ufw.Web.Client.Features.KnownHosts;
using Ufw.Web.Client.Features.NetworkInterfaces;
using Ufw.Web.Client.Features.Rules.Authoring;
using Ufw.Web.Client.Services.Errors;
using Ufw.Web.Model.V1.KnownHosts;
using Ufw.Web.Model.V1.NetworkInterfaces;
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

        Assert.IsNull(data.KnownHosts.Error);
        Assert.IsNull(data.Interfaces.Error);
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
        errors.Setup(mapper => mapper.CanDescribe(failure)).Returns(true);
        errors.Setup(mapper => mapper.Describe(failure)).Returns(clientError);
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        Assert.AreSame(clientError, data.KnownHosts.Error);
        Assert.IsEmpty(data.KnownHosts.Items);
        Assert.IsNull(data.Interfaces.Error);
        CollectionAssert.AreEqual(new[] { networkInterface }, data.VisibleInterfaces.ToArray());
        Assert.IsFalse(service.IsUnknownInterface(data, "eno1"));
        errors.Verify(mapper => mapper.Describe(failure), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_InterfaceFailureSuppressesUnknownInterfaceWarningsAsync()
    {
        HttpRequestException failure = new("interfaces unavailable");
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new();
        KnownHostInventoryItem host = Host("db", FirewallAddressFamily.IPv4, isVisible: true);
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse { Hosts = [host] });
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        errors.Setup(mapper => mapper.CanDescribe(failure)).Returns(true);
        errors.Setup(mapper => mapper.Describe(failure)).Returns(new ClientError(ClientErrorKind.Unavailable, "interface lookup failed", true));
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        Assert.IsNull(data.KnownHosts.Error);
        CollectionAssert.AreEqual(new[] { host }, service.GetVisibleKnownHosts(data, ipv6Enabled: true).ToArray());
        Assert.AreEqual("interface lookup failed", data.Interfaces.Error?.Message);
        Assert.IsEmpty(data.Interfaces.Items);
        Assert.IsEmpty(data.VisibleInterfaces);
        Assert.IsFalse(service.IsUnknownInterface(data, "eno1"));
        errors.Verify(mapper => mapper.Describe(failure), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_EmptySuccessfulCatalogsAreNotMarkedUnavailableAsync()
    {
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new(MockBehavior.Strict);
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new KnownHostInventoryResponse());
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventoryResponse());
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        Assert.IsNull(data.KnownHosts.Error);
        Assert.IsNull(data.Interfaces.Error);
        Assert.IsEmpty(data.KnownHosts.Items);
        Assert.IsEmpty(data.Interfaces.Items);
        Assert.IsTrue(service.IsUnknownInterface(data, "eno1"));
    }

    [TestMethod]
    public async Task LoadAsync_IndependentCatalogFailuresRetainBothDiagnosticsAsync()
    {
        HttpRequestException hostFailure = new("host lookup failed");
        HttpRequestException interfaceFailure = new("interface lookup failed");
        ClientError hostError = new(ClientErrorKind.Unavailable, "hosts offline", Retryable: true);
        ClientError interfaceError = new(ClientErrorKind.Forbidden, "interfaces forbidden", Retryable: false);
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new();
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(hostFailure);
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(interfaceFailure);
        errors.Setup(mapper => mapper.CanDescribe(hostFailure)).Returns(true);
        errors.Setup(mapper => mapper.CanDescribe(interfaceFailure)).Returns(true);
        errors.Setup(mapper => mapper.Describe(hostFailure)).Returns(hostError);
        errors.Setup(mapper => mapper.Describe(interfaceFailure)).Returns(interfaceError);
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        RuleEditorReferenceData data = await service.LoadAsync();

        Assert.AreSame(hostError, data.KnownHosts.Error);
        Assert.AreSame(interfaceError, data.Interfaces.Error);
        Assert.IsEmpty(data.KnownHosts.Items);
        Assert.IsEmpty(data.Interfaces.Items);
        Assert.IsFalse(service.IsUnknownInterface(data, "eno1"));
        errors.Verify(mapper => mapper.Describe(hostFailure), Times.Once);
        errors.Verify(mapper => mapper.Describe(interfaceFailure), Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_CancellationIsPropagatedRatherThanReportedAsCatalogFailureAsync()
    {
        using CancellationTokenSource lifetime = new();
        await lifetime.CancelAsync();
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new(MockBehavior.Strict);
        knownHosts.Setup(service => service.RefreshAsync(lifetime.Token)).ThrowsAsync(new OperationCanceledException(lifetime.Token));
        networkInterfaces.Setup(service => service.RefreshAsync(lifetime.Token)).ReturnsAsync(new NetworkInterfaceInventoryResponse());
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.LoadAsync(lifetime.Token));
        errors.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task LoadAsync_UnclassifiedErrorDoesNotBecomeAnEmptyCatalogAsync()
    {
        InvalidOperationException failure = new("unexpected lookup failure");
        Mock<IKnownHostInventoryService> knownHosts = new();
        Mock<INetworkInterfaceInventoryService> networkInterfaces = new();
        Mock<IClientErrorMapper> errors = new();
        knownHosts.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        networkInterfaces.Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventoryResponse());
        errors.Setup(mapper => mapper.CanDescribe(failure)).Returns(false);
        RuleEditorReferenceDataService service = new(knownHosts.Object, networkInterfaces.Object, errors.Object);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.LoadAsync());
        errors.Verify(mapper => mapper.Describe(It.IsAny<Exception>()), Times.Never);
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
