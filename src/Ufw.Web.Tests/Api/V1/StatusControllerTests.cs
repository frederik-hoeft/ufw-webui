using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Status;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class StatusControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetStatusAsync_ForwardsDedicatedDaemonProbeAsync()
    {
        Mock<IStatusDaemonGateway> daemonStatus = new();
        daemonStatus.Setup(static c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success());
        StatusController controller = CreateController(daemonStatus.Object);

        IActionResult result = await controller.GetStatusAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<NoContentResult>(result);
        daemonStatus.Verify(static c => c.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStatusAsync_DaemonFailurePropagatesToExceptionBoundaryAsync()
    {
        UfwIpcException expected = new(StatusCodes.Status503ServiceUnavailable, "daemon unavailable");
        Mock<IStatusDaemonGateway> daemonStatus = new();
        daemonStatus.Setup(static c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Failure(expected));
        StatusController controller = CreateController(daemonStatus.Object);

        UfwIpcException actual = await Assert.ThrowsExactlyAsync<UfwIpcException>(
            () => controller.GetStatusAsync(TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
    }

    private static StatusController CreateController(IStatusDaemonGateway daemonStatus) => new(daemonStatus)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        }
    };
}
