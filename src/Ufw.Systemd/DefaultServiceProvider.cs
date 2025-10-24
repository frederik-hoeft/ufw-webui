using Jab;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Services;

namespace Ufw.Systemd;

[ServiceProvider]
[Import<IConfigurationModule>]
[Transient<INamedServiceProvider, NamedServiceProvider>]
internal sealed partial class DefaultServiceProvider;
