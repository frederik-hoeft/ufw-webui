using Jab;
using Ufw.Ipc.Shared.Transport.Security;

namespace Ufw.Systemd.Transport.Security;

[ServiceProviderModule]
[Singleton<ITransportSecurityService, ServerTransportSecurityService>]
internal interface ITransportSecurityModule;
