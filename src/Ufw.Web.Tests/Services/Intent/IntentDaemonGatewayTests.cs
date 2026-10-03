using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.Intent;

namespace Ufw.Web.Tests.Services.Intent;

[TestClass]
public sealed class IntentDaemonGatewayTests
{
    [TestMethod]
    public async Task GetContextAsync_UsesExpectedDaemonEndpointAsync()
    {
        IntentContextResponse expected = new(1, "deployment-test");
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        IntentDaemonGateway gateway = new(client.Object);

        DaemonResult<IntentContextResponse> result = await gateway.GetContextAsync();

        Assert.AreSame(expected, result.Result);
        client.VerifyAll();
    }
}
