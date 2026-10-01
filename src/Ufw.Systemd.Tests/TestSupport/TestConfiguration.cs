using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.TestSupport;

internal sealed class TestConfiguration(AppSettings settings) : IConfiguration
{
    public AppSettings Settings { get; } = settings;

    public ValueTask LoadAsync(string settingsPath, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Test configurations are already initialized.");
}
