using Jab;

namespace Ufw.Systemd.Persistence;

[ServiceProviderModule]
[Singleton<IDurableFileStore, DurableFileStore>]
internal interface IPersistenceModule;
