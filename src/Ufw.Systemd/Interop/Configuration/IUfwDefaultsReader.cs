using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Configuration;

internal interface IUfwDefaultsReader
{
    ValueTask<FirewallConfigurationSnapshot?> ReadAsync(CancellationToken cancellationToken);
}
