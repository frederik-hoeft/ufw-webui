using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
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
        Mock<IUfwClient> client = new();
        client.Setup(static c => c.SendAsync(RequestMethod.Get, "/api/v1/status", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        StatusController controller = CreateController(client.Object);

        IActionResult result = await controller.GetStatusAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<NoContentResult>(result);
        client.Verify(static c => c.SendAsync(RequestMethod.Get, "/api/v1/status", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStatusAsync_MapsDaemonFailureAsync()
    {
        Mock<IUfwClient> client = new();
        client.Setup(static c => c.SendAsync(RequestMethod.Get, "/api/v1/status", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status503ServiceUnavailable, "daemon unavailable"));
        StatusController controller = CreateController(client.Object);

        IActionResult result = await controller.GetStatusAsync(TestContext.CancellationToken);

        ObjectResult problem = Assert.IsInstanceOfType<ObjectResult>(result);
        Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
    }

    private static StatusController CreateController(IUfwClient client) => new(client, new DaemonApiErrorMapper())
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        }
    };
}
