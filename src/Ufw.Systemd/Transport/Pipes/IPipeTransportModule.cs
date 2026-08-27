using Jab;
using Ufw.Systemd.Transport.Pipes.Unix;

namespace Ufw.Systemd.Transport.Pipes;

[ServiceProviderModule]
[Singleton<INamedPipeServerStreamDescriptor, UnixNamedPipeServerStreamDescriptor>]
[Singleton<ITransportLayerService, NamedPipeServerTransportService>]
internal interface IPipeTransportModule;
