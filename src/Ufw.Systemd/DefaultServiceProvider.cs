using Jab;
using Ufw.Shared.Security.Certificates;
using Ufw.Systemd.Api;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Network;
using Ufw.Systemd.NetworkInterfaces;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Transport;
using Ufw.Systemd.Transport.Security;

namespace Ufw.Systemd;

[ServiceProvider]
[Import<IConfigurationModule>]
[Import<INetworkModule>]
[Import<INetworkInterfaceModule>]
[Import<ITransportModule>]
[Import<ITransportSecurityModule>]
[Import<IApiModule>]
[Import<IFirewallModule>]
[Singleton<ILogger, ConsoleLogger>]
[Singleton<ICertificateLoader, PemCertificateLoader>]
internal sealed partial class DefaultServiceProvider;
