using System.Net;
using System.Net.Sockets;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Tests.TestSupport;
using Ufw.Systemd.Transport.Tcp;

namespace Ufw.Systemd.Tests.Transport.Tcp;

[TestClass]
public sealed class TcpServerStreamDescriptorTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public void Constructor_BindsConfiguredEndpoint()
    {
        int port = FindAvailablePort(IPAddress.Loopback);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(transportType: TransportType.Tcp, tcpPort: port));
        using TcpServerStreamDescriptor descriptor = new(configuration);
        using TcpListener competingListener = new(IPAddress.Loopback, port);

        Assert.ThrowsExactly<SocketException>(competingListener.Start);
    }

    [TestMethod]
    public async Task ServeAsync_ListensOnConfiguredIpv4EndpointAsync() =>
        await AssertListensOnConfiguredEndpointAsync(IPAddress.Loopback);

    [TestMethod]
    public async Task ServeAsync_ListensOnConfiguredIpv6EndpointAsync()
    {
        if (!Socket.OSSupportsIPv6)
        {
            Assert.Inconclusive("IPv6 is not supported by this host.");
        }

        await AssertListensOnConfiguredEndpointAsync(IPAddress.IPv6Loopback);
    }

    private async Task AssertListensOnConfiguredEndpointAsync(IPAddress address)
    {
        int port = FindAvailablePort(address);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(transportType: TransportType.Tcp, tcpListenAddress: address.ToString(), tcpPort: port));
        using TcpServerStreamDescriptor descriptor = new(configuration);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        Task<NetworkStream> serverTask = descriptor.ServeAsync(timeout.Token);
        using TcpClient client = new(address.AddressFamily);
        await client.ConnectAsync(address, port, timeout.Token);
        await using NetworkStream server = await serverTask;

        Assert.IsTrue(client.Connected);
        Assert.IsTrue(server.CanRead);
        Assert.IsTrue(server.CanWrite);
    }

    private static int FindAvailablePort(IPAddress address)
    {
        using TcpListener listener = new(address, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
