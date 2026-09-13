using Ufw.Shared.Firewall;
using Ufw.Systemd.Interop.Configuration;

namespace Ufw.Ipc.Tests.Support;

internal sealed class StaticUfwDefaultsReader : IUfwDefaultsReader
{
    public ValueTask<FirewallConfigurationSnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<FirewallConfigurationSnapshot?>(TestFirewallConfiguration.Enabled);
    }
}
