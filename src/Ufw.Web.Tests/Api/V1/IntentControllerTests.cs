using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Api.V1.Controllers;
using Ufw.Web.Api.V1.Errors;
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
            .ReturnsAsync(expected);

        IntentController controller = CreateController(daemonIntent.Object);
        ActionResult<IntentContextResponse> result = await controller.GetContextAsync(TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task GetContextAsync_MapsDaemonFailureAsync()
    {
        Mock<IIntentDaemonGateway> daemonIntent = new();
        daemonIntent
            .Setup(static c => c.GetContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status500InternalServerError, "context unavailable"));

        IntentController controller = CreateController(daemonIntent.Object);
        ActionResult<IntentContextResponse> result = await controller.GetContextAsync(TestContext.CancellationToken);

        ObjectResult problem = (ObjectResult)result.Result!;
        Assert.AreEqual(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    private static IntentController CreateController(IIntentDaemonGateway daemonIntent)
    {
        IntentController controller = new(daemonIntent, new DaemonApiErrorMapper())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }
}
