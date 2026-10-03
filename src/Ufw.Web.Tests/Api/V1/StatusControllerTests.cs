using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Web.Services.Status;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Errors;

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
            .Returns(Task.CompletedTask);
        StatusController controller = CreateController(daemonStatus.Object);

        IActionResult result = await controller.GetStatusAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<NoContentResult>(result);
        daemonStatus.Verify(static c => c.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStatusAsync_MapsDaemonFailureAsync()
    {
        Mock<IStatusDaemonGateway> daemonStatus = new();
        daemonStatus.Setup(static c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status503ServiceUnavailable, "daemon unavailable"));
        StatusController controller = CreateController(daemonStatus.Object);

        IActionResult result = await controller.GetStatusAsync(TestContext.CancellationToken);

        ObjectResult problem = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
    }

    private static StatusController CreateController(IStatusDaemonGateway daemonStatus) => new(daemonStatus, new DaemonApiErrorMapper())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        }
    };
}
