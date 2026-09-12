using Jab;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

[ServiceProviderModule]
[Singleton<TimeProvider>(Factory = nameof(GetTimeProvider))]
[Singleton<IChildProcessRunner, DefaultChildProcessRunner>]
[Singleton<IUfwRunner, UfwRunner>]
[Singleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>]
[Singleton<IAuthorizedKeyStore, FileAuthorizedKeyStore>]
[Singleton<INonceStore, FileNonceStore>]
[Singleton<IDeploymentIdentityProvider, FileDeploymentIdentityProvider>]
[Singleton<IIntentVerifier, IntentVerifier>]
[Singleton<IUfwExecutionGate, UfwExecutionGate>]
[Singleton<IRuleReorderPlanner, RuleReorderPlanner>]
[Singleton<IRuleReinsertabilityClassifier, RuleReinsertabilityClassifier>]
[Singleton<IReorderRecoveryJournal, FileReorderRecoveryJournal>]
[Singleton<IRuleReorderRecoveryCoordinator, RuleReorderRecoveryCoordinator>]
[Singleton<IFirewallMutationSafetyGuard, FirewallMutationSafetyGuard>]
[Singleton<IFirewallReorderExecutor, FirewallReorderExecutor>]
[Singleton<IFirewallReorderService, FirewallReorderService>]
[Singleton<IFirewallReorderRecoveryService, FirewallReorderRecoveryService>]
[Singleton<IFirewallRuleSnapshotReader, FirewallRuleSnapshotReader>]
[Singleton<IFirewallRuleQueryService, FirewallRuleQueryService>]
[Singleton<IFirewallRuleInterfaceValidator, FirewallRuleInterfaceValidator>]
[Singleton<IFirewallMutationExecutor, FirewallMutationExecutor>]
[Singleton<IFirewallMutationService, FirewallMutationService>]
internal interface IFirewallModule
{
    internal static TimeProvider GetTimeProvider() => TimeProvider.System;
}
