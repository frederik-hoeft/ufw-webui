using Jab;
using Ufw.Pipes.Shared.Security.Certificates;
using Ufw.Systemd.Api;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Network;
using Ufw.Systemd.Services;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Transport.Pipes;
using Ufw.Systemd.Transport.Security;
using Ufw.Systemd.Transport.Security.CertificateValidation;

namespace Ufw.Systemd;

[ServiceProvider]
[Import<IConfigurationModule>]
[Import<INetworkModule>]
[Import<IPipeTransportModule>]
[Import<ITransportSecurityModule>]
[Import<IApiModule>]
[Singleton<ILogger, ConsoleLogger>]
[Singleton<ICertificateLoader, PemCertificateLoader>]
[Singleton<IRemoteCertificateValidationHandler, MutualTlsRemoteCertificateValidationHandler>]
[Transient<INamedServiceProvider, NamedServiceProvider>]
internal sealed partial class DefaultServiceProvider;
