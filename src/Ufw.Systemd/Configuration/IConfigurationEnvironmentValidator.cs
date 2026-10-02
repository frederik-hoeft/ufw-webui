using Ufw.Systemd.Configuration.Model;

namespace Ufw.Systemd.Configuration;

internal interface IConfigurationEnvironmentValidator
{
    void ThrowIfInvalid(AppSettings settings);
}
