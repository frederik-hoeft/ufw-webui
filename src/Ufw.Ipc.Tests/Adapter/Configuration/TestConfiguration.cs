using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Ipc.Tests.Adapter.Configuration;

/// <summary>
/// In-memory <see cref="IConfiguration"/> that is already initialized for the test host.
/// </summary>
internal sealed class TestConfiguration(AppSettings settings) : IConfiguration
{
    public AppSettings Settings { get; } = settings;

    public ValueTask LoadAsync(string settingsPath, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Test configurations are already initialized.");
}
