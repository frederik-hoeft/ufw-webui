using System.Security.Authentication;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Ipc.Tests.Adapter.Configuration;

/// <summary>
/// Builds daemon <see cref="AppSettings"/> suitable for in-process tests without touching the host filesystem.
/// </summary>
internal static class TestAppSettingsFactory
{
    public static AppSettings Create(
        TimeSpan? ioTimeout = null,
        TimeSpan? requestTimeout = null,
        int maxConnections = 2,
        bool debugMode = true,
        bool tlsEnabled = false,
        SslProtocols sslProtocols = SslProtocols.None,
        RemoteCertificateValidationOptions? remoteCertificateValidation = null,
        string? serverCertificatePath = null,
        string? serverCertificateKeyPath = null,
        SecurityOptions? security = null) =>
        new()
        {
            DebugMode = debugMode,
            ExposeRemoteExceptionDetails = false,
            // Never executed by the in-process adapter; value is only present to satisfy the model shape.
            UfwPath = "/nonexistent/ufw-for-tests",
            UfwDefaultsPath = "/nonexistent/ufw-defaults-for-tests",
            Pipe = new PipeOptions
            {
                PipeName = "/tmp/ufw-ipc-tests.inprocess",
                TlsEnabled = tlsEnabled,
                SslProtocols = sslProtocols,
                RemoteCertificateValidation = remoteCertificateValidation,
                ServerCertificatePath = serverCertificatePath,
                ServerCertificateKeyPath = serverCertificateKeyPath,
            },
            Network = new NetworkOptions
            {
                MaxConnections = maxConnections,
                IoTimeout = ioTimeout ?? TimeSpan.FromSeconds(15),
                RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15),
            },
            Security = security,
        };
}
