using System.Security.Authentication;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.TestSupport;

internal static class TestAppSettingsFactory
{
    public static AppSettings Create(
        string? authorizedKeysPath = null,
        string? nonceStorePath = null,
        string? deploymentIdPath = null,
        string? reorderRecoveryJournalPath = null,
        string? ufwDefaultsPath = null,
        string? pipeName = null,
        TransportType transportType = TransportType.Pipe,
        string tcpListenAddress = "127.0.0.1",
        int tcpPort = 1234,
        bool tlsEnabled = false,
        SslProtocols sslProtocols = SslProtocols.None,
        RemoteCertificateValidationOptions? remoteCertificateValidation = null,
        string? serverCertificatePath = null,
        string? serverCertificateKeyPath = null,
        bool debugMode = true,
        bool exposeRemoteExceptionDetails = false,
        int maxConnections = 8) =>
        new()
        {
            DebugMode = debugMode,
            ExposeRemoteExceptionDetails = exposeRemoteExceptionDetails,
            UfwPath = "/usr/sbin/ufw",
            UfwDefaultsPath = ufwDefaultsPath ?? "/nonexistent/ufw-defaults",
            Transport = new TransportOptions
            {
                Type = transportType,
                Pipe = transportType is TransportType.Pipe
                    ? new PipeOptions { PipeName = pipeName ?? "/tmp/ufw-systemd-tests.pipe" }
                    : null,
                Tcp = transportType is TransportType.Tcp
                    ? new TcpOptions { ListenAddress = tcpListenAddress, Port = tcpPort }
                    : null,
                Security = new TransportSecurityOptions
                {
                    TlsEnabled = tlsEnabled,
                    SslProtocols = sslProtocols,
                    RemoteCertificateValidation = remoteCertificateValidation,
                    ServerCertificatePath = serverCertificatePath,
                    ServerCertificateKeyPath = serverCertificateKeyPath,
                },
            },
            Network = new NetworkOptions
            {
                MaxConnections = maxConnections,
                IoTimeout = TimeSpan.FromSeconds(30),
                RequestTimeout = TimeSpan.FromMinutes(30),
            },
            Security = new SecurityOptions
            {
                AuthorizedKeysPath = authorizedKeysPath ?? "/nonexistent/authorized_keys",
                NonceStorePath = nonceStorePath ?? "/nonexistent/intent-nonces",
                DeploymentIdPath = deploymentIdPath ?? "/nonexistent/deployment-id",
                ReorderRecoveryJournalPath = reorderRecoveryJournalPath ?? "/nonexistent/reorder-recovery.json",
                MaxIntentAge = TimeSpan.FromMinutes(5),
                ClockSkew = TimeSpan.FromSeconds(30),
            },
        };
}
