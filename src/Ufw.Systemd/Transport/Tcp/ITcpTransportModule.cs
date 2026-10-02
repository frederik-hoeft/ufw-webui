using Jab;

namespace Ufw.Systemd.Transport.Tcp;

[ServiceProviderModule]
[Singleton<ITcpServerStreamDescriptor, TcpServerStreamDescriptor>]
[Singleton<ITcpTransportLayerService, TcpServerTransportService>]
internal interface ITcpTransportModule;
