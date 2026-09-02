using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Ufw.Ipc.Client;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Web.Api.V1.Controllers;

namespace Ufw.Web.Tests.Api.V1;

[TestClass]
public sealed class IntentControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetContextAsync_ForwardsDaemonContextAsync()
    {
        Mock<IUfwClient> client = new();
        IntentContextResponse expected = new(1, "deployment-test");
        client
            .Setup(static c => c.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        IntentController controller = CreateController(client.Object);
        ActionResult<IntentContextResponse> result = await controller.GetContextAsync(TestContext.CancellationToken);

        OkObjectResult ok = (OkObjectResult)result.Result!;
        Assert.AreSame(expected, ok.Value);
    }

    [TestMethod]
    public async Task GetContextAsync_MapsDaemonFailureAsync()
    {
        Mock<IUfwClient> client = new();
        client
            .Setup(static c => c.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UfwIpcException(StatusCodes.Status500InternalServerError, "context unavailable"));

        IntentController controller = CreateController(client.Object);
        ActionResult<IntentContextResponse> result = await controller.GetContextAsync(TestContext.CancellationToken);

        ObjectResult problem = (ObjectResult)result.Result!;
        Assert.AreEqual(StatusCodes.Status500InternalServerError, problem.StatusCode);
    }

    private static IntentController CreateController(IUfwClient client)
    {
        IntentController controller = new(client)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }
}
