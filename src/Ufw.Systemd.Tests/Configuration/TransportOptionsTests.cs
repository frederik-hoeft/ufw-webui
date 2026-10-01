using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.Configuration;

[TestClass]
public sealed class TransportOptionsTests
{
    [TestMethod]
    public void ThrowIfInvalid_RequiresSelectedPipeConfiguration()
    {
        TransportOptions options = CreateOptions(TransportType.Pipe, pipe: null, tcp: new TcpOptions { ListenAddress = "127.0.0.1", Port = 1234 });

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_RequiresSelectedTcpConfiguration()
    {
        TransportOptions options = CreateOptions(TransportType.Tcp, pipe: new PipeOptions { PipeName = "/tmp/ufw.pipe" }, tcp: null);

        Assert.ThrowsExactly<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [TestMethod]
    public void ThrowIfInvalid_AllowsOnlySelectedConfigurationToBePresent()
    {
        CreateOptions(TransportType.Pipe, pipe: new PipeOptions { PipeName = "/tmp/ufw.pipe" }, tcp: null).ThrowIfInvalid();
        CreateOptions(TransportType.Tcp, pipe: null, tcp: new TcpOptions { ListenAddress = "127.0.0.1", Port = 1234 }).ThrowIfInvalid();
    }

    private static TransportOptions CreateOptions(TransportType type, PipeOptions? pipe, TcpOptions? tcp) =>
        new()
        {
            Type = type,
            Pipe = pipe,
            Tcp = tcp,
            Security = new TransportSecurityOptions
            {
                TlsEnabled = false,
                SslProtocols = System.Security.Authentication.SslProtocols.None,
                RemoteCertificateValidation = null,
                ServerCertificatePath = null,
                ServerCertificateKeyPath = null,
            },
        };
}
