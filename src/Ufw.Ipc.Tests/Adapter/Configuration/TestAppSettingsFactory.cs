using Ufw.Systemd.Configuration.Model;

namespace Ufw.Ipc.Tests.Adapter.Configuration;

/// <summary>
/// Builds daemon <see cref="AppSettings"/> suitable for in-process tests without touching the host filesystem.
/// </summary>
internal static class TestAppSettingsFactory
{
    public static AppSettings Create(TimeSpan? ioTimeout = null, TimeSpan? requestTimeout = null, int maxConnections = 2, bool debugMode = true) =>
        new()
        {
            DebugMode = debugMode,
            // Never executed by the in-process adapter; value is only present to satisfy the model shape.
            UfwPath = "/nonexistent/ufw-for-tests",
            WriteToConsole = false,
            Pipe = new PipeOptions
            {
                PipeName = "/tmp/ufw-ipc-tests.inprocess",
                ServerCertificatePath = "/nonexistent/cert.pem",
                ServerCertificateKeyPath = "/nonexistent/key.pem",
            },
            Network = new NetworkOptions
            {
                MaxConnections = maxConnections,
                IoTimeout = ioTimeout ?? TimeSpan.FromSeconds(15),
                RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(15),
            },
        };
}
