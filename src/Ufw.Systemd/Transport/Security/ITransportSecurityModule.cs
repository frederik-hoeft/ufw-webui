using Jab;
using Ufw.Pipes.Shared.Transport.Security;

namespace Ufw.Systemd.Transport.Security;

[ServiceProviderModule]
// TODO: temporarily disable transport security until we have certificates set up
//[Singleton<ITransportSecurityService, ServerTransportSecurityService>]
[Singleton<ITransportSecurityService, NoTransportSecurityService>]
internal interface ITransportSecurityModule;