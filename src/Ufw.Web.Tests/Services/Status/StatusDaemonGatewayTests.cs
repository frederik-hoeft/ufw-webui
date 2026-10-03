using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Web.Services.Status;

namespace Ufw.Web.Tests.Services.Status;

[TestClass]
public sealed class StatusDaemonGatewayTests
{
    [TestMethod]
    public async Task GetStatusAsync_UsesExpectedDaemonEndpointAsync()
    {
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(static c => c.SendAsync(RequestMethod.Get, "/api/v1/status", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        StatusDaemonGateway gateway = new(client.Object);

        await gateway.GetStatusAsync();

        client.VerifyAll();
    }
}
