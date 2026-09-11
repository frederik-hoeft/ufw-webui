using Jab;

namespace Ufw.Systemd.NetworkInterfaces;

[ServiceProviderModule]
[Singleton<INetworkInterfaceProvider, SystemNetworkInterfaceProvider>]
[Singleton<INetworkInterfaceSnapshotService, NetworkInterfaceSnapshotService>]
internal interface INetworkInterfaceModule;
