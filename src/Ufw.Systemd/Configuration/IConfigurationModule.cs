using Jab;

namespace Ufw.Systemd.Configuration;

[ServiceProviderModule]
[Singleton<AppSettingsJsonSerializerContext>(Factory = nameof(GetAppSettingsJsonSerializerContext))]
[Singleton<IConfigurationEnvironmentValidator, ConfigurationEnvironmentValidator>]
[Singleton<IConfiguration, ConfigurationImpl>]
internal interface IConfigurationModule
{
    internal static AppSettingsJsonSerializerContext GetAppSettingsJsonSerializerContext() => AppSettingsJsonSerializerContext.Default;
}
