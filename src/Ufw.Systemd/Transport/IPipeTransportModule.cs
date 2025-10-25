using Jab;
using Ufw.Pipes.Shared.Transport.Security;
using Ufw.Systemd.Transport.Pipes;
using Ufw.Systemd.Transport.Pipes.Unix;
using Ufw.Systemd.Transport.Security;

namespace Ufw.Systemd.Transport;

[ServiceProviderModule]
[Singleton<INamedPipeServerStreamDescriptor, UnixNamedPipeServerStreamDescriptor>]
[Singleton<ITransportLayerService, NamedPipeServerTransportService>]
[Singleton<ITransportSecurityService, ServerTransportSecurityService>]
internal interface IPipeTransportModule;