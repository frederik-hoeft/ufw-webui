using Jab;

namespace Ufw.Systemd.Transport.Tcp;

[ServiceProviderModule]
[Singleton<ITcpServerStreamDescriptor, TcpServerStreamDescriptor>]
[Singleton<ITransportLayerService, TcpServerTransportService>]
internal interface ITcpTransportModule;
