using Microsoft.AspNetCore.Mvc;
using Moq;
using System.ComponentModel.DataAnnotations;
using Ufw.Shared.Management.NetworkInterfaces;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Model.V1.NetworkInterfaces;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class NetworkInterfacesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_ReturnsCachedInventoryWithoutReconcilingAsync()
    {
        IReadOnlyList<NetworkInterfaceInventoryItem> interfaces = [new NetworkInterfaceInventoryItem(Guid.CreateVersion7(), "eno1", "service VLAN", isVisible: true)];
        DateTimeOffset reconciledAt = new(2026, 9, 8, 20, 0, 0, TimeSpan.Zero);
        Mock<INetworkInterfaceInventoryService> inventory = new();
        inventory.Setup(service => service.GetCachedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventorySnapshot(interfaces, reconciledAt));
        NetworkInterfacesController controller = new(inventory.Object);

        ActionResult<NetworkInterfaceInventoryResponse> result = await controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        NetworkInterfaceInventoryResponse response = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(ok.Value);
        Assert.AreSame(interfaces, response.Interfaces);
        Assert.AreEqual(reconciledAt, response.ReconciledAt);
        inventory.Verify(service => service.ReconcileAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ReconcileAsync_ReturnsInventoryResponseAsync()
    {
        DateTimeOffset reconciledAt = DateTimeOffset.UtcNow;
        Mock<INetworkInterfaceInventoryService> inventory = new();
        inventory.Setup(service => service.ReconcileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventorySnapshot([], reconciledAt));
        NetworkInterfacesController controller = new(inventory.Object);

        IActionResult result = await controller.ReconcileAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result);
        NetworkInterfaceInventoryResponse response = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(ok.Value);
        Assert.AreEqual(reconciledAt, response.ReconciledAt);
    }

    [TestMethod]
    public async Task UpdateVisibilityAsync_UpdatesByPublicIdAsync()
    {
        Mock<INetworkInterfaceInventoryService> inventory = new();
        Guid id = Guid.CreateVersion7();
        NetworkInterfaceInventoryItem item = new(id, "docker0", null, isVisible: false);
        inventory.Setup(service => service.UpdateVisibilityAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventorySnapshot([item], null));
        NetworkInterfacesController controller = new(inventory.Object);

        IActionResult result = await controller.UpdateVisibilityAsync(id, new UpdateNetworkInterfaceVisibilityRequest { IsVisible = false }, TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result);
        NetworkInterfaceInventoryResponse response = Assert.IsInstanceOfType<NetworkInterfaceInventoryResponse>(ok.Value);
        Assert.AreSame(item, response.Interfaces.Single());
    }

    [TestMethod]
    public async Task GetStaleAsync_ReturnsRetainedMetadataAsync()
    {
        NetworkInterfaceInventoryItem stale = new(Guid.CreateVersion7(), "docker0", "old bridge", isVisible: false);
        Mock<INetworkInterfaceInventoryService> inventory = new();
        inventory.Setup(service => service.GetStaleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceCleanupResult([stale], null));
        NetworkInterfacesController controller = new(inventory.Object);

        ActionResult<NetworkInterfaceCleanupResponse> result = await controller.GetStaleAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        NetworkInterfaceCleanupResponse response = Assert.IsInstanceOfType<NetworkInterfaceCleanupResponse>(ok.Value);
        Assert.AreSame(stale, response.StaleInterfaces.Single());
        Assert.AreEqual(0, response.RemovedCount);
    }

    [TestMethod]
    public async Task CleanupStaleAsync_DelegatesSelectionAndReturnsRemovalCountAsync()
    {
        Guid id = Guid.CreateVersion7();
        Mock<INetworkInterfaceInventoryService> inventory = new();
        inventory.Setup(service => service.CleanupStaleAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { id })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetworkInterfaceCleanupResult([], null, RemovedCount: 1));
        NetworkInterfacesController controller = new(inventory.Object);

        ActionResult<NetworkInterfaceCleanupResponse> result = await controller.CleanupStaleAsync(new CleanupNetworkInterfacesRequest { InterfaceIds = [id] }, TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        NetworkInterfaceCleanupResponse response = Assert.IsInstanceOfType<NetworkInterfaceCleanupResponse>(ok.Value);
        Assert.AreEqual(1, response.RemovedCount);
    }

    [TestMethod]
    public void UpdateCommentRequest_UsesSharedRawLimit()
    {
        UpdateNetworkInterfaceCommentRequest exact = new() { Comment = new string('x', NetworkInterfaceLimits.MAX_COMMENT_LENGTH) };
        UpdateNetworkInterfaceCommentRequest padded = new() { Comment = $" {new string('x', NetworkInterfaceLimits.MAX_COMMENT_LENGTH)} " };

        Assert.IsTrue(IsValid(exact));
        Assert.IsFalse(IsValid(padded));
    }

    [TestMethod]
    public void CleanupRequest_RequiresAtLeastOneNonEmptyIdentity()
    {
        Assert.IsFalse(IsValid(new CleanupNetworkInterfacesRequest()));
        Assert.IsFalse(IsValid(new CleanupNetworkInterfacesRequest { InterfaceIds = [Guid.Empty] }));
        Assert.IsTrue(IsValid(new CleanupNetworkInterfacesRequest { InterfaceIds = [Guid.CreateVersion7()] }));
    }

    private static bool IsValid(object value)
    {
        List<ValidationResult> errors = [];
        return Validator.TryValidateObject(value, new ValidationContext(value), errors, validateAllProperties: true);
    }
}
