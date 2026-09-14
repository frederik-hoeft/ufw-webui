using Ufw.Shared.Firewall;

namespace Ufw.Systemd.Interop.Configuration;

internal static class UfwDefaultsParser
{
    public static bool TryParse(string contents, out FirewallConfigurationSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(contents);
        return UfwDefaultsGrammar.Instance.TryParse(contents, out snapshot);
    }
}
