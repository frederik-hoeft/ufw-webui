using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.Services.NetworkInterfaces;

[TestClass]
public sealed class DaemonNetworkInterfaceSourceTests
{
    [TestMethod]
    public async Task GetInterfaceNamesAsync_UsesExpectedDaemonEndpointAndReturnsOrdinalOrderingAsync()
    {
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(ufw => ufw.SendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetworkInterfaceListResponse(["wlan0", "eno1", "docker0"]));
        DaemonNetworkInterfaceSource source = new(client.Object);

        IReadOnlyList<string> result = await source.GetInterfaceNamesAsync();

        CollectionAssert.AreEqual(new[] { "docker0", "eno1", "wlan0" }, result.ToArray());
        client.VerifyAll();
    }

    [TestMethod]
    public async Task GetInterfaceNamesAsync_RejectsMissingBlankDuplicateAndOversizedNamesAsync()
    {
        IReadOnlyList<string>?[] invalid =
        [
            null,
            ["eno1", " "],
            ["eno1", "eno1"],
            [new string('x', NetworkInterfaceEntry.MAX_NAME_LENGTH + 1)],
        ];

        foreach (IReadOnlyList<string>? names in invalid)
        {
            Mock<IUfwClient> client = new();
            client.Setup(ufw => ufw.SendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NetworkInterfaceListResponse(names!));
            DaemonNetworkInterfaceSource source = new(client.Object);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => source.GetInterfaceNamesAsync());
        }
    }

    [TestMethod]
    public async Task GetInterfaceNamesAsync_CaseDistinctLinuxInterfaceNamesRemainDistinctAsync()
    {
        Mock<IUfwClient> client = new();
        client.Setup(ufw => ufw.SendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NetworkInterfaceListResponse(["eno1", "ENO1"]));
        DaemonNetworkInterfaceSource source = new(client.Object);

        IReadOnlyList<string> result = await source.GetInterfaceNamesAsync();

        CollectionAssert.AreEqual(new[] { "ENO1", "eno1" }, result.ToArray());
    }
}
