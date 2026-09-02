using Jab;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

[ServiceProviderModule]
[Singleton<TimeProvider>(Factory = nameof(GetTimeProvider))]
[Singleton<IChildProcessRunner, DefaultChildProcessRunner>]
[Singleton<IUfwRunner, UfwRunner>]
[Singleton<IAuthorizedKeyStore, FileAuthorizedKeyStore>]
[Singleton<INonceStore, FileNonceStore>]
[Singleton<IIntentVerifier, IntentVerifier>]
[Singleton<IUfwExecutionGate, UfwExecutionGate>]
[Singleton<IFirewallMutationService, FirewallMutationService>]
internal interface IFirewallModule
{
    internal static TimeProvider GetTimeProvider() => TimeProvider.System;
}
