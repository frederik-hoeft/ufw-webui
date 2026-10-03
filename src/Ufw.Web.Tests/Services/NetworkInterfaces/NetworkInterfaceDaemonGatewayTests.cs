using Moq;
using Ufw.Ipc.Client;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Web.Data.Model;
using Ufw.Web.Services.Daemon;
using Ufw.Web.Services.NetworkInterfaces;

namespace Ufw.Web.Tests.Services.NetworkInterfaces;

[TestClass]
public sealed class NetworkInterfaceDaemonGatewayTests
{
    [TestMethod]
    public async Task GetInterfaceNamesAsync_UsesExpectedDaemonEndpointAndReturnsOrdinalOrderingAsync()
    {
        Mock<IUfwClient> client = new(MockBehavior.Strict);
        client.Setup(ufw => ufw.TrySendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UfwIpcResult<NetworkInterfaceListResponse>.Success(new NetworkInterfaceListResponse(["wlan0", "eno1", "docker0"])));
        NetworkInterfaceDaemonGateway gateway = new(client.Object);

        DaemonResult<IReadOnlyList<string>> result = await gateway.GetInterfaceNamesAsync();

        CollectionAssert.AreEqual(new[] { "docker0", "eno1", "wlan0" }, result.Result.ToArray());
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
            client.Setup(ufw => ufw.TrySendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
                .ReturnsAsync(UfwIpcResult<NetworkInterfaceListResponse>.Success(new NetworkInterfaceListResponse(names!)));
            NetworkInterfaceDaemonGateway gateway = new(client.Object);

            await Assert.ThrowsExactlyAsync<DaemonInvalidResponseException>(() => gateway.GetInterfaceNamesAsync());
        }
    }

    [TestMethod]
    public async Task GetInterfaceNamesAsync_DaemonFailureIsReturnedForCallerClassificationAsync()
    {
        UfwIpcError expected = new(500, "enumeration failed");
        Mock<IUfwClient> client = new();
        client.Setup(ufw => ufw.TrySendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>())).ReturnsAsync(UfwIpcResult<NetworkInterfaceListResponse>.Failure(expected));
        NetworkInterfaceDaemonGateway gateway = new(client.Object);

        DaemonResult<IReadOnlyList<string>> result = await gateway.GetInterfaceNamesAsync();

        Assert.AreSame(expected, result.Error);
    }

    [TestMethod]
    public async Task GetInterfaceNamesAsync_CaseDistinctLinuxInterfaceNamesRemainDistinctAsync()
    {
        Mock<IUfwClient> client = new();
        client.Setup(ufw => ufw.TrySendAsync<NetworkInterfaceListResponse>(RequestMethod.Get, "/api/v1/network-interfaces", It.IsAny<CancellationToken>()))
            .ReturnsAsync(UfwIpcResult<NetworkInterfaceListResponse>.Success(new NetworkInterfaceListResponse(["eno1", "ENO1"])));
        NetworkInterfaceDaemonGateway gateway = new(client.Object);

        DaemonResult<IReadOnlyList<string>> result = await gateway.GetInterfaceNamesAsync();

        CollectionAssert.AreEqual(new[] { "ENO1", "eno1" }, result.Result.ToArray());
    }
}
