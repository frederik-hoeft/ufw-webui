using Jab;
using Ufw.Systemd.Configuration.Providers;

namespace Ufw.Systemd.Configuration;

[ServiceProviderModule]
[Singleton<AppSettingsJsonSerializerContext>(Factory = nameof(GetAppSettingsJsonSerializerContext))]
[Singleton<IConfiguration, ConfigurationImpl>]
[Singleton<IResourceProvider, ResourceProvider>]
[Singleton<IResourceProviderStrategy, FileSystemResourceProviderStrategy>]
internal interface IConfigurationModule
{
    internal static AppSettingsJsonSerializerContext GetAppSettingsJsonSerializerContext() => AppSettingsJsonSerializerContext.Default;
}
