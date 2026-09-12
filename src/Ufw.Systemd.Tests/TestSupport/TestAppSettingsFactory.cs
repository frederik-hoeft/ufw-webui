using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.TestSupport;

internal static class TestAppSettingsFactory
{
    public static AppSettings Create(
        string? authorizedKeysPath = null,
        string? nonceStorePath = null,
        string? deploymentIdPath = null,
        string? reorderRecoveryJournalPath = null) =>
        new()
        {
            DebugMode = true,
            UfwPath = "/usr/sbin/ufw",
            WriteToConsole = false,
            Pipe = new PipeOptions
            {
                PipeName = "/tmp/ufw-systemd-tests.pipe",
            },
            Network = new NetworkOptions(),
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
