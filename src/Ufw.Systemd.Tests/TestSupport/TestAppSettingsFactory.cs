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
        bool debugMode = true,
        bool exposeRemoteExceptionDetails = false) =>
        new()
        {
            DebugMode = debugMode,
            ExposeRemoteExceptionDetails = exposeRemoteExceptionDetails,
            UfwPath = "/usr/sbin/ufw",
            UfwDefaultsPath = ufwDefaultsPath ?? "/nonexistent/ufw-defaults",
            Pipe = new PipeOptions
            {
                PipeName = pipeName ?? "/tmp/ufw-systemd-tests.pipe",
                TlsEnabled = false,
                SslProtocols = System.Security.Authentication.SslProtocols.None,
                RemoteCertificateValidation = null,
                ServerCertificatePath = null,
                ServerCertificateKeyPath = null,
            },
            Network = new NetworkOptions
            {
                MaxConnections = 8,
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
