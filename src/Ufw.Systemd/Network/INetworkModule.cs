using Jab;

namespace Ufw.Systemd.Network;

[ServiceProviderModule]
[Singleton<INetworkApplication, NetworkApplication>]
[Transient<INetworkApplicationWorker, NetworkApplicationWorker>]
internal interface INetworkModule;