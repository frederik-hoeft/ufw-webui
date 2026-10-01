using Jab;
using Ufw.Shared.Ipc.Transport.Security;
using Ufw.Systemd.Transport.Security.CertificateValidation;

namespace Ufw.Systemd.Transport.Security;

[ServiceProviderModule]
[Singleton(typeof(ServerTransportSecurityOptionsSnapshot))]
[Singleton<IRemoteCertificateValidationHandler, MutualTlsRemoteCertificateValidationHandler>]
[Singleton<ITransportSecurityService, ServerTransportSecurityService>]
internal interface ITransportSecurityModule;
