using Jab;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall.Deletion;
using Ufw.Systemd.Firewall.Insertion;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Firewall.Replacement;
using Ufw.Systemd.Interop.Configuration;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Firewall;

[ServiceProviderModule]
[Singleton<TimeProvider>(Factory = nameof(GetTimeProvider))]
[Singleton<IChildProcessRunner, DefaultChildProcessRunner>]
[Singleton<IUfwRunner, UfwRunner>]
[Singleton<IUfwProcessExecutor, UfwProcessExecutor>]
[Singleton<IUfwDefaultsReader, UfwDefaultsReader>]
[Singleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>]
[Singleton<IAuthorizedKeyStore, FileAuthorizedKeyStore>]
[Singleton<INonceStore, FileNonceStore>]
[Singleton<IDeploymentIdentityProvider, FileDeploymentIdentityProvider>]
[Singleton<IIntentEnvelopeVerifier, IntentEnvelopeVerifier>]
[Singleton<IIntentPayloadBinder<AddRulePayload>, AddIntentPayloadBinder>]
[Singleton<IIntentPayloadBinder<DeleteRulePayload>, DeleteIntentPayloadBinder>]
[Singleton<IIntentPayloadBinder<BatchDeleteRulesPayload>, BatchDeleteIntentPayloadBinder>]
[Singleton<IIntentPayloadBinder<InsertRulePayload>, InsertIntentPayloadBinder>]
[Singleton<IIntentPayloadBinder<ReorderRulesPayload>, ReorderIntentPayloadBinder>]
[Singleton<IIntentPayloadBinder<ReplaceRulePayload>, ReplaceIntentPayloadBinder>]
[Singleton<IIntentVerifier, IntentVerifier>]
[Singleton<IUfwExecutionGate, UfwExecutionGate>]
[Singleton<ISignedMutationOrchestrator, SignedMutationOrchestrator>]
[Singleton<IRuleReorderPlanner, RuleReorderPlanner>]
[Singleton<IRuleReinsertionCostProvider, UfwArgumentCountReinsertionCostProvider>]
[Singleton<IRuleReinsertabilityClassifier, RuleReinsertabilityClassifier>]
[Singleton<IFirewallReorderPreflightEvaluator, FirewallReorderPreflightEvaluator>]
[Singleton<IFirewallReorderMoveExecutor, FirewallReorderMoveExecutor>]
[Singleton<IReorderRecoveryJournal, FileReorderRecoveryJournal>]
[Singleton<IRuleReorderRecoveryCoordinator, RuleReorderRecoveryCoordinator>]
[Singleton<IFirewallMutationSafetyGuard, FirewallMutationSafetyGuard>]
[Singleton<IFirewallBatchDeleteExecutor, FirewallBatchDeleteExecutor>]
[Singleton<IFirewallBatchDeleteService, FirewallBatchDeleteService>]
[Singleton<IFirewallReorderExecutor, FirewallReorderExecutor>]
[Singleton<IFirewallReorderService, FirewallReorderService>]
[Singleton<IFirewallReorderRecoveryService, FirewallReorderRecoveryService>]
[Singleton<IFirewallOrderedInsertionExecutor, FirewallOrderedInsertionExecutor>]
[Singleton<IFirewallOrderedInsertionService, FirewallOrderedInsertionService>]
[Singleton<IFirewallRuleReplacementExecutor, FirewallRuleReplacementExecutor>]
[Singleton<IFirewallRuleReplacementService, FirewallRuleReplacementService>]
[Singleton<IFirewallRuleSnapshotReader, FirewallRuleSnapshotReader>]
[Singleton<IFirewallRuleQueryService, FirewallRuleQueryService>]
[Singleton<IFirewallRuleInterfaceValidator, FirewallRuleInterfaceValidator>]
[Singleton<IFirewallRuleCapabilityValidator, FirewallRuleCapabilityValidator>]
[Singleton<IFirewallMutationExecutor, FirewallMutationExecutor>]
[Singleton<IFirewallMutationService, FirewallMutationService>]
internal interface IFirewallModule
{
    internal static TimeProvider GetTimeProvider() => TimeProvider.System;
}
