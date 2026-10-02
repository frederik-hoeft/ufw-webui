using System.Text.Json;
using Moq;
using Ufw.Shared.Ipc.Transport;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Tests.TestSupport;
using Ufw.Systemd.Transport;
using Ufw.Systemd.Transport.Pipes;
using Ufw.Systemd.Transport.Tcp;

namespace Ufw.Systemd.Tests.Transport;

[TestClass]
public sealed class ConfiguredTransportLayerServiceTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServeAsync_ResolvesAndDelegatesOnlyToStartupSelectedTransportAsync(bool useTcp)
    {
        TransportType transportType = useTcp ? TransportType.Tcp : TransportType.Pipe;
        Type selectedServiceType = useTcp ? typeof(ITcpTransportLayerService) : typeof(IPipeTransportLayerService);
        Type unselectedServiceType = useTcp ? typeof(IPipeTransportLayerService) : typeof(ITcpTransportLayerService);
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(transportType: transportType));
        Mock<ITransportLayerService> selectedTransport = new(MockBehavior.Strict);
        Mock<ITransportLayerConnection> connection = new(MockBehavior.Strict);
        Mock<IServiceProvider> serviceProvider = new(MockBehavior.Strict);
        serviceProvider.Setup(provider => provider.GetService(selectedServiceType)).Returns(selectedTransport.Object);
        selectedTransport.Setup(transport => transport.ServeAsync(CancellationToken.None)).ReturnsAsync(connection.Object);

        ConfiguredTransportLayerService service = new(configuration, serviceProvider.Object);
        ITransportLayerConnection result = await service.ServeAsync(CancellationToken.None);

        Assert.AreSame(connection.Object, result);
        serviceProvider.Verify(provider => provider.GetService(selectedServiceType), Times.Once);
        serviceProvider.Verify(provider => provider.GetService(unselectedServiceType), Times.Never);
        selectedTransport.Verify(transport => transport.ServeAsync(CancellationToken.None), Times.Once);
    }

    [TestMethod]
    public async Task DefaultServiceProvider_ResolvesConfiguredPipeTransportAsync()
    {
        string ufwPath = Path.GetTempFileName();
        string settingsPath = Path.GetTempFileName();
        AppSettings settings = TestAppSettingsFactory.Create(authorizedKeysPath: null, nonceStorePath: null, deploymentIdPath: null, reorderRecoveryJournalPath: null);
        settings = new AppSettings
        {
            DebugMode = settings.DebugMode,
            ExposeRemoteExceptionDetails = settings.ExposeRemoteExceptionDetails,
            UfwPath = ufwPath,
            UfwDefaultsPath = settings.UfwDefaultsPath,
            Transport = settings.Transport,
            Network = settings.Network,
            Security = null,
        };

        try
        {
            await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings, AppSettingsJsonSerializerContext.Default.AppSettings));
            await using DefaultServiceProvider serviceProvider = new();
            IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
            await configuration.LoadAsync(settingsPath, CancellationToken.None);

            ITransportLayerService transport = serviceProvider.GetService<ITransportLayerService>();

            Assert.IsInstanceOfType<ConfiguredTransportLayerService>(transport);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(ufwPath);
        }
    }
}
