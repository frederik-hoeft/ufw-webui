using ConsoleAppFramework;
using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Network;

namespace Ufw.Systemd;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Command methods cannot be static")]
internal sealed class Commands
{
    [Command("serve")]
    public async Task ServeAsync(string config = "/etc/ufw-manager/settings.json", CancellationToken cancellationToken = default)
    {
        await using DefaultServiceProvider serviceProvider = new();
        IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
        bool success = await configuration.TryReloadAsync(config, cancellationToken);
        if (!success)
        {
            await Console.Error.WriteLineAsync("Failed to load service configuration from /etc/ufw-manager/settings.json");
            throw new InvalidOperationException("failed to load service configuration");
        }
        INetworkApplication networkApp = serviceProvider.GetService<INetworkApplication>();
        await networkApp.RunAsync(cancellationToken);
    }
}
