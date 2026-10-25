using Moq;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Web.Client.Api;
using Ufw.Web.Client.Api.Intent;
using Ufw.Web.Client.Features.Rules.Intent;

namespace Ufw.Web.Client.Tests.Features.Rules.Intent;

[TestClass]
public sealed class CompatibleIntentContextProviderTests
{
    [TestMethod]
    public async Task GetDeploymentIdAsync_ReturnsCurrentDeploymentWithoutCachingAsync()
    {
        Mock<IIntentContextApiClient> client = new(MockBehavior.Strict);
        client.SetupSequence(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "first"))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION, "second"));
        CompatibleIntentContextProvider provider = new(client.Object);

        Assert.AreEqual("first", await provider.GetDeploymentIdAsync(TestContext.CancellationToken));
        Assert.AreEqual("second", await provider.GetDeploymentIdAsync(TestContext.CancellationToken));
        client.Verify(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetDeploymentIdAsync_RejectsIncompatibleProtocolAsync()
    {
        Mock<IIntentContextApiClient> client = new(MockBehavior.Strict);
        client.Setup(candidate => candidate.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntentContextResponse(IntentProtocol.VERSION + 1, "deployment"));
        CompatibleIntentContextProvider provider = new(client.Object);

        ApiProtocolException exception = await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => provider.GetDeploymentIdAsync(TestContext.CancellationToken));
        Assert.Contains("protocol mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    public required TestContext TestContext { get; set; }
}
