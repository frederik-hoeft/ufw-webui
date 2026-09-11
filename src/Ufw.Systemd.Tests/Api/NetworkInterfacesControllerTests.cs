using Moq;
using System.Net.NetworkInformation;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Systemd.Api.Controllers;
using Ufw.Systemd.NetworkInterfaces;
using Ufw.Systemd.Services.Logging;

namespace Ufw.Systemd.Tests.Api;

[TestClass]
public sealed class NetworkInterfacesControllerTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task GetNetworkInterfaces_ReturnsProviderSnapshotAsync()
    {
        Mock<INetworkInterfaceProvider> provider = new();
        string[] names = ["eno1", "enp4s0f2.1100", "lo"];
        provider.Setup(static service => service.GetInterfaceNames()).Returns(names);
        NetworkInterfaceSnapshotService snapshots = new(provider.Object, new ConsoleLogger());
        NetworkInterfacesController controller = new(snapshots);

        IResponsePayload response = await controller.GetNetworkInterfacesAsync(TestContext.CancellationToken);

        NetworkInterfaceListResponse inventory = Assert.IsInstanceOfType<NetworkInterfaceListResponse>(response);
        CollectionAssert.AreEqual(names, inventory.Interfaces.ToArray());
    }

    [TestMethod]
    public async Task GetNetworkInterfaces_EnumerationFailureReturnsInternalServerErrorAsync()
    {
        Mock<INetworkInterfaceProvider> provider = new();
        provider
            .Setup(static service => service.GetInterfaceNames())
            .Throws(new NetworkInformationException());
        NetworkInterfaceSnapshotService snapshots = new(provider.Object, new ConsoleLogger());
        NetworkInterfacesController controller = new(snapshots);

        IResponsePayload response = await controller.GetNetworkInterfacesAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<InternalServerErrorResponse>(response);
    }
}
