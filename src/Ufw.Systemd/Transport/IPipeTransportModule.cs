using Jab;
using Ufw.Pipes.Shared.Transport.Security;
using Ufw.Systemd.Transport.Pipes;
using Ufw.Systemd.Transport.Pipes.Unix;
using Ufw.Systemd.Transport.Security;

namespace Ufw.Systemd.Transport;

[ServiceProviderModule]
[Singleton<INamedPipeServerStreamDescriptor, UnixNamedPipeServerStreamDescriptor>]
[Singleton<ITransportLayerService, NamedPipeServerTransportService>]
// TODO: temporarily disable transport security until we have certificates set up
//[Singleton<ITransportSecurityService, ServerTransportSecurityService>]
[Singleton<ITransportSecurityService, NoTransportSecurityService>]
internal interface IPipeTransportModule;