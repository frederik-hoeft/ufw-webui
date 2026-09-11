using Moq;
using Ufw.Client.Api;
using Ufw.Client.NetworkInterfaces;

namespace Ufw.Client.Tests.NetworkInterfaces;

[TestClass]
public sealed class NetworkInterfaceInventoryServiceTests
{
    [TestMethod]
    public async Task RefreshAsync_NormalizesCommentsAndOrdersVisibleInterfacesFirstAsync()
    {
        Mock<INetworkInterfaceApiClient> api = new();
        Guid zId = Guid.NewGuid();
        Guid aId = Guid.NewGuid();
        Guid hiddenId = Guid.NewGuid();
        api.Setup(client => client.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new NetworkInterfaceInventoryResponse
        {
            Interfaces =
            [
                new() { Id = hiddenId, Name = "docker0", Comment = "   ", IsVisible = false },
                new() { Id = zId, Name = "wlan0", Comment = " wireless ", IsVisible = true },
                new() { Id = aId, Name = "eno1", IsVisible = true },
            ],
        });
        NetworkInterfaceInventoryService service = new(api.Object);

        NetworkInterfaceInventoryResponse response = await service.RefreshAsync();

        CollectionAssert.AreEqual(new[] { "eno1", "wlan0", "docker0" }, response.Interfaces.Select(static item => item.Name).ToArray());
        Assert.AreEqual("wireless", response.Interfaces.Single(item => item.Id == zId).Comment);
        Assert.IsNull(response.Interfaces.Single(item => item.Id == hiddenId).Comment);
        Assert.AreSame(response, service.Current);
    }

    [TestMethod]
    public async Task RefreshAsync_RejectsInvalidOrDuplicateEntriesWithoutReplacingCurrentAsync()
    {
        Mock<INetworkInterfaceApiClient> api = new();
        Guid id = Guid.NewGuid();
        api.SetupSequence(client => client.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetworkInterfaceInventoryResponse { Interfaces = [new() { Id = id, Name = "eno1", IsVisible = true }] })
            .ReturnsAsync(new NetworkInterfaceInventoryResponse { Interfaces = [new() { Id = id, Name = "eno1" }, new() { Id = Guid.NewGuid(), Name = "eno1" }] });
        NetworkInterfaceInventoryService service = new(api.Object);
        NetworkInterfaceInventoryResponse initial = await service.RefreshAsync();

        await Assert.ThrowsExactlyAsync<ApiProtocolException>(() => service.RefreshAsync());

        Assert.AreSame(initial, service.Current);
    }

    [TestMethod]
    public async Task MutationOperations_NormalizeAndPublishReturnedSnapshotAsync()
    {
        Mock<INetworkInterfaceApiClient> api = new();
        Guid id = Guid.NewGuid();
        NetworkInterfaceInventoryResponse response = new() { Interfaces = [new() { Id = id, Name = "eno1", Comment = " note ", IsVisible = true }] };
        api.Setup(client => client.UpdateCommentAsync(id, "note", It.IsAny<CancellationToken>())).ReturnsAsync(response);
        api.Setup(client => client.UpdateVisibilityAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        api.Setup(client => client.ReconcileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(response);
        NetworkInterfaceInventoryService service = new(api.Object);

        Assert.AreEqual("note", (await service.UpdateCommentAsync(id, "note")).Interfaces[0].Comment);
        Assert.AreEqual("note", (await service.UpdateVisibilityAsync(id, false)).Interfaces[0].Comment);
        Assert.AreEqual("note", (await service.ReconcileAsync()).Interfaces[0].Comment);
    }
}
