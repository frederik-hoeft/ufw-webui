using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Tests.TestSupport;

internal sealed class TestConfiguration(AppSettings settings) : IConfiguration
{
    public AppSettings Settings { get; set; } = settings;

    public ValueTask<bool> TryReloadAsync(string settingsPath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(true);
}
