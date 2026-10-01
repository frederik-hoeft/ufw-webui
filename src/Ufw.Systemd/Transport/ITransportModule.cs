using Jab;
using Ufw.Systemd.Transport.Pipes;
using Ufw.Systemd.Transport.Tcp;

namespace Ufw.Systemd.Transport;

[ServiceProviderModule]
[Import<IPipeTransportModule>]
[Import<ITcpTransportModule>]
[Singleton<ITransportLayerService, ConfiguredTransportLayerService>]
internal interface ITransportModule;
