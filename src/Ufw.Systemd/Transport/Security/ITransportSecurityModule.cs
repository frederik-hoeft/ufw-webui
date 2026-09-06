using Jab;
using Ufw.Shared.Ipc.Transport.Security;

namespace Ufw.Systemd.Transport.Security;

[ServiceProviderModule]
[Singleton<ITransportSecurityService, ServerTransportSecurityService>]
internal interface ITransportSecurityModule;
