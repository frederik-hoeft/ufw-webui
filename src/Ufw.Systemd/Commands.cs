using ConsoleAppFramework;
using System.Diagnostics.CodeAnalysis;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Network;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd;

[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Command methods cannot be static")]
internal sealed class Commands
{
    [Command("serve")]
    public async Task ServeAsync(string config = "/etc/ufw-manager/settings.json", CancellationToken cancellationToken = default)
    {
        await using DefaultServiceProvider serviceProvider = new();
        IConfiguration configuration = serviceProvider.GetService<IConfiguration>();
        await configuration.LoadAsync(config, cancellationToken);
        IFirewallReorderRecoveryService reorderRecovery = serviceProvider.GetService<IFirewallReorderRecoveryService>();
        await reorderRecovery.RecoverAsync(cancellationToken);

        // Resolve the key store before exposing the network listener so configured key material is validated and snapshotted for the daemon lifetime.
        // Key-file changes intentionally require a restart and therefore must not be picked up lazily by the first signed mutation request.
        _ = serviceProvider.GetService<IAuthorizedKeyStore>();
        INetworkApplication networkApp = serviceProvider.GetService<INetworkApplication>();
        await networkApp.RunAsync(cancellationToken);
    }
}
