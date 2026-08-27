using Ufw.Systemd.Configuration;
using Ufw.Systemd.Configuration.Model;

namespace Ufw.Ipc.Tests.Adapter.Configuration;

/// <summary>
/// In-memory <see cref="IConfiguration"/> that never loads from disk.
/// </summary>
internal sealed class TestConfiguration(AppSettings settings) : IConfiguration
{
    public AppSettings Settings { get; private set; } = settings;

    public void ReplaceSettings(AppSettings settings) => Settings = settings;

    public ValueTask<bool> TryReloadAsync(string settingsPath, CancellationToken cancellationToken) =>
        ValueTask.FromResult(false);
}
