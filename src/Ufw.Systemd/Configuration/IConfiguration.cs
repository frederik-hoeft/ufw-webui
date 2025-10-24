using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Configuration;

internal interface IConfiguration
{
    AppSettings Settings { get; }

    ValueTask<bool> TryReloadAsync(string settingsPath, CancellationToken cancellationToken);
}
