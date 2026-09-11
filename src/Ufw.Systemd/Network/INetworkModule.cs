using Jab;

namespace Ufw.Systemd.Network;

[ServiceProviderModule]
[Singleton<INetworkApplication, NetworkApplication>]
[Singleton<INetworkConnectionProcessor, NetworkConnectionProcessor>]
[Singleton<INetworkApplicationWorker, NetworkApplicationWorker>]
internal interface INetworkModule;
