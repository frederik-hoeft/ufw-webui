using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Errors;
using Ufw.Web.Api.V1.Models.NetworkInterfaces;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class NetworkInterfacesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetAsync_ReturnsCachedInventoryWithoutReconcilingAsync()
    {
        Mock<INetworkInterfaceInventoryService> inventory = new();
        NetworkInterfaceInventoryResponse expected = new(
            [new NetworkInterfaceInventoryItem(Guid.CreateVersion7(), "eno1", "service VLAN", IsVisible: true)],
            new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero));
        inventory.Setup(service => service.GetCachedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        NetworkInterfacesController controller = CreateController(inventory.Object);

        ActionResult<NetworkInterfaceInventoryResponse> result = await controller.GetAsync(TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result.Result);
        Assert.AreSame(expected, ok.Value);
        inventory.Verify(service => service.ReconcileAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ReconcileAsync_MapsDaemonFailureToBadGatewayAsync()
    {
        Mock<INetworkInterfaceInventoryService> inventory = new();
        inventory
            .Setup(service => service.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status500InternalServerError, "daemon enumeration failed"));
        NetworkInterfacesController controller = CreateController(inventory.Object);

        IActionResult result = await controller.ReconcileAsync(TestContext.CancellationToken);

        ObjectResult problem = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status502BadGateway, problem.StatusCode);
    }

    [TestMethod]
    public async Task UpdateVisibilityAsync_UpdatesByPublicIdAsync()
    {
        Mock<INetworkInterfaceInventoryService> inventory = new();
        Guid id = Guid.CreateVersion7();
        NetworkInterfaceInventoryResponse expected = new([new NetworkInterfaceInventoryItem(id, "docker0", null, IsVisible: false)], new DateTimeOffset(2026, 9, 8, 20, 0, 0, TimeSpan.Zero));
        inventory
            .Setup(service => service.UpdateVisibilityAsync(id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        NetworkInterfacesController controller = CreateController(inventory.Object);

        IActionResult result = await controller.UpdateVisibilityAsync(
            id,
            new UpdateNetworkInterfaceVisibilityRequest { IsVisible = false },
            TestContext.CancellationToken);

        OkObjectResult ok = Assert.IsInstanceOfType<OkObjectResult>(result);
        Assert.AreSame(expected, ok.Value);
    }

    private static NetworkInterfacesController CreateController(INetworkInterfaceInventoryService inventory) =>
        new(inventory, new DaemonApiErrorMapper())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };
}
