using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Configuration;

internal interface IConfiguration
{
    AppSettings Settings { get; }

    ValueTask LoadAsync(string settingsPath, CancellationToken cancellationToken);
}
