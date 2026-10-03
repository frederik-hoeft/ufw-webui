using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Intent;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class IntentControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetContextAsync_ForwardsDaemonContextAsync()
    {
        Mock<IIntentDaemonGateway> daemonIntent = new();
        IntentContextResponse expected = new(1, "deployment-test");
        daemonIntent
            .Setup(static c => c.GetContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Success(expected));

        IntentController controller = CreateController(daemonIntent.Object);
        ActionResult<IntentContextResponse> result = await controller.GetContextAsync(TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task GetContextAsync_DaemonFailurePropagatesToExceptionBoundaryAsync()
    {
        UfwIpcException expected = new(StatusCodes.Status500InternalServerError, "context unavailable");
        Mock<IIntentDaemonGateway> daemonIntent = new();
        daemonIntent
            .Setup(static c => c.GetContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DaemonResult.Failure<IntentContextResponse>(expected));

        IntentController controller = CreateController(daemonIntent.Object);
        UfwIpcException actual = await Assert.ThrowsExactlyAsync<UfwIpcException>(
            () => controller.GetContextAsync(TestContext.CancellationToken));

        Assert.AreSame(expected, actual);
    }

    private static IntentController CreateController(IIntentDaemonGateway daemonIntent) => new(daemonIntent)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        }
    };
}
