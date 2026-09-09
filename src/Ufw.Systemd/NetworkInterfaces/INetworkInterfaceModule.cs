using Jab;

namespace Ufw.Systemd.NetworkInterfaces;

[ServiceProviderModule]
[Singleton<INetworkInterfaceProvider, SystemNetworkInterfaceProvider>]
internal interface INetworkInterfaceModule;
