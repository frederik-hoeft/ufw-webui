using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Cryptography;
using Ufw.Ipc.Client;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Configuration;
using Ufw.Ipc.Tests.Adapter.DependencyInjection;
using Ufw.Ipc.Tests.Support;
using Ufw.Roslyn.Controllers.Mapping;
using Ufw.Shared.Firewall;
using Ufw.Shared.Firewall.Rendering;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Ipc.Serialization;
using Ufw.Shared.Ipc.Serialization.Json;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Api;
using Ufw.Systemd.Api.Controllers;
using Ufw.Systemd.Configuration.Model;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Deletion;
using Ufw.Systemd.Firewall.Insertion;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Firewall.Replacement;
using Ufw.Systemd.Interop.Configuration;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;
using DaemonConfiguration = Ufw.Systemd.Configuration.IConfiguration;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
[DoNotParallelize]
public sealed class RuleReplacementExecutionIntegrationTests : IpcProtocolTestBase
{
    private string _temporaryDirectory = null!;
    private string _mockStatePath = null!;
    private string _authorizedKeysPath = null!;
    private ECDsa _signingKey = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), nameof(RuleReplacementExecutionIntegrationTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _mockStatePath = Path.Combine(_temporaryDirectory, "ufw-state.json");
        _authorizedKeysPath = Path.Combine(_temporaryDirectory, "authorized_keys");
        _signingKey = IntentSigner.CreateP256();
        File.WriteAllText(_authorizedKeysPath, _signingKey.ExportSubjectPublicKeyInfoPem());
    }

    [TestCleanup]
    public void Cleanup()
    {
        _signingKey.Dispose();
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    protected override ValueTask ConfigureServerServicesAsync(IServiceCollection services, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SecurityOptions security = new()
        {
            AuthorizedKeysPath = _authorizedKeysPath,
            NonceStorePath = Path.Combine(_temporaryDirectory, "intent-nonces"),
            DeploymentIdPath = Path.Combine(_temporaryDirectory, "deployment-id"),
            ReorderRecoveryJournalPath = Path.Combine(_temporaryDirectory, "reorder-recovery.json"),
            MaxIntentAge = TimeSpan.FromMinutes(5),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        AppSettings settings = TestAppSettingsFactory.Create(security: security);

        services.RemoveAll<DaemonConfiguration>();
        services.RemoveAll<IApiEndpointMap<IRequestMessage, IResponseMessage>>();
        services.AddSingleton<DaemonConfiguration>(new TestConfiguration(settings));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IUfwRunner>(new MockBackedUfwRunner(_mockStatePath));
        services.AddSingleton<IUfwProcessExecutor, UfwProcessExecutor>();
        services.AddSingleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>();
        services.AddSingleton<IAuthorizedKeyStore, FileAuthorizedKeyStore>();
        services.AddSingleton<INonceStore, FileNonceStore>();
        services.AddSingleton<IDeploymentIdentityProvider, FileDeploymentIdentityProvider>();
        services.AddProductionIntentVerification();
        services.AddSingleton<IUfwExecutionGate, UfwExecutionGate>();
        services.AddSingleton<IRuleReorderPlanner, RuleReorderPlanner>();
        services.AddSingleton<IRuleReinsertionCostProvider, UfwArgumentCountReinsertionCostProvider>();
        services.AddSingleton<IRuleReinsertabilityClassifier, RuleReinsertabilityClassifier>();
        services.AddSingleton<IFirewallReorderPreflightEvaluator, FirewallReorderPreflightEvaluator>();
        services.AddSingleton<IFirewallReorderMoveExecutor, FirewallReorderMoveExecutor>();
        services.AddSingleton<IReorderRecoveryJournal, FileReorderRecoveryJournal>();
        services.AddSingleton<IUfwDefaultsReader, StaticUfwDefaultsReader>();
        services.AddSingleton<IFirewallRuleSnapshotReader, FirewallRuleSnapshotReader>();
        services.AddSingleton<IRuleReorderRecoveryCoordinator, RuleReorderRecoveryCoordinator>();
        services.AddSingleton<IFirewallMutationSafetyGuard, FirewallMutationSafetyGuard>();
        services.AddSingleton<ISignedMutationOrchestrator, SignedMutationOrchestrator>();
        services.AddSingleton<IFirewallBatchDeleteExecutor, FirewallBatchDeleteExecutor>();
        services.AddSingleton<IFirewallBatchDeleteService, FirewallBatchDeleteService>();
        services.AddSingleton<IFirewallReorderExecutor, FirewallReorderExecutor>();
        services.AddSingleton<IFirewallReorderService, FirewallReorderService>();
        services.AddSingleton<IFirewallRuleInterfaceValidator, AlwaysValidInterfaceValidator>();
        services.AddSingleton<IFirewallRuleCapabilityValidator, FirewallRuleCapabilityValidator>();
        services.AddSingleton<IFirewallOrderedInsertionExecutor, FirewallOrderedInsertionExecutor>();
        services.AddSingleton<IFirewallOrderedInsertionService, FirewallOrderedInsertionService>();
        services.AddSingleton<IFirewallRuleReplacementPreflightEvaluator, FirewallRuleReplacementPreflightEvaluator>();
        services.AddSingleton<IFirewallRuleReplacementTransactionExecutor, FirewallRuleReplacementTransactionExecutor>();
        services.AddSingleton<IFirewallRuleReplacementExecutor, FirewallRuleReplacementExecutor>();
        services.AddSingleton<IFirewallRuleReplacementService, FirewallRuleReplacementService>();
        services.AddSingleton<IFirewallRuleQueryService, FirewallRuleQueryService>();
        services.AddSingleton<IFirewallMutationService, UnsupportedMutationService>();
        services.AddScoped<IntentController>();
        services.AddScoped<RulesController>();
        services.AddSingleton<IApiEndpointMap<IRequestMessage, IResponseMessage>, UfwApiEndpointMap>();
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task SignedReplacement_PreservesPositionAndRejectsReplayAsync()
    {
        await SeedRuleAsync("80");
        await SeedRuleAsync("22");
        await SeedRuleAsync("443");
        await EnableAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            ReplaceRuleRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, 1, Rule("53"));

            RuleReplacementResponse response = await context.Client.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, cancellationToken);

            Assert.AreEqual(RuleReplacementOutcome.Completed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsNotNull(response.ReplacementRule);
            Assert.AreEqual("53", response.ReplacementRule.Rule!.DestinationPorts);
            Assert.AreEqual("53", response.FinalSnapshot.Rules[1].Rule!.DestinationPorts);
            Assert.AreEqual("80", response.FinalSnapshot.Rules[0].Rule!.DestinationPorts);
            Assert.AreEqual("443", response.FinalSnapshot.Rules[2].Rule!.DestinationPorts);

            UfwIpcException replay = await Assert.ThrowsExactlyAsync<UfwIpcException>(async () =>
                await context.Client.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, cancellationToken));
            Assert.AreEqual(409, replay.StatusCode);
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SignedSameIdentityReplacement_UpdatesAndRemovesCommentWithoutReorderingAsync()
    {
        await SeedRuleAsync("22", "old");
        await EnableAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            ReplaceRuleRequest updateRequest = CreateSignedRequest(intentContext.DeploymentId, baseline, 0, Rule("22", "new"));

            RuleReplacementResponse updated = await context.Client.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(updateRequest, cancellationToken);

            Assert.AreEqual(RuleReplacementOutcome.Completed, updated.Outcome);
            Assert.AreEqual("new", updated.ReplacementRule!.Rule!.Comment);
            RuleListResponse updatedBaseline = updated.FinalSnapshot!;
            ReplaceRuleRequest removeRequest = CreateSignedRequest(intentContext.DeploymentId, updatedBaseline, 0, Rule("22"));

            RuleReplacementResponse removed = await context.Client.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(removeRequest, cancellationToken);

            Assert.AreEqual(RuleReplacementOutcome.Completed, removed.Outcome);
            Assert.IsNull(removed.ReplacementRule!.Rule!.Comment);
            Assert.HasCount(1, removed.FinalSnapshot!.Rules);
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task ReplacementThatWouldCreateSemanticDuplicate_IsRejectedWithoutMutationAsync()
    {
        await SeedRuleAsync("80");
        await SeedRuleAsync("22");
        await EnableAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            ReplaceRuleRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, 0, Rule("22"));

            RuleReplacementResponse response = await context.Client.SendAsync<ReplaceRuleRequest, RuleReplacementResponse>(request, cancellationToken);

            Assert.AreEqual(RuleReplacementOutcome.PreconditionFailed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsNull(response.ReplacementRule);
            Assert.AreEqual(FirewallRuleSnapshotFingerprint.Compute(baseline), FirewallRuleSnapshotFingerprint.Compute(response.FinalSnapshot));
        }, cancellationToken: TestContext.CancellationToken);
    }

    private ReplaceRuleRequest CreateSignedRequest(string deploymentId, RuleListResponse baseline, int targetOccurrenceId, FirewallRuleSpecification replacement)
    {
        ReplaceRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline),
            TargetOccurrenceId = targetOccurrenceId,
            OriginalRuleId = baseline.Rules[targetOccurrenceId].RuleId!,
            ReplacementRule = replacement,
        };
        return IntentRequestFactory.CreateReplaceRequest(_signingKey, deploymentId, payload, MessageJsonSerializerContext.Default.ReplaceRulePayload, TimeProvider.System);
    }

    private static Task<RuleListResponse> GetRulesAsync(IIpcTestContext context, CancellationToken cancellationToken) =>
        context.Client.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);

    private static Task<IntentContextResponse> GetIntentContextAsync(IIpcTestContext context, CancellationToken cancellationToken) =>
        context.Client.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", cancellationToken);

    private async Task SeedRuleAsync(string port, string? comment = null)
    {
        List<string> arguments = ["allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", port, "proto", "tcp"];
        if (comment is not null)
        {
            arguments.Add("comment");
            arguments.Add(comment);
        }
        MockCommandResult result = await MockBackedUfwRunner.InvokeAsync(_mockStatePath, arguments, TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode);
    }

    private async Task EnableAsync()
    {
        MockCommandResult enabled = await MockBackedUfwRunner.InvokeAsync(_mockStatePath, ["--force", "enable"], TestContext.CancellationToken);
        Assert.AreEqual(0, enabled.ExitCode);
    }

    private static FirewallRuleSpecification Rule(string port, string? comment = null) => new()
    {
        Action = FirewallAction.Allow,
        AddressFamily = FirewallAddressFamily.IPv4,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = port,
        Comment = comment,
    };

    private sealed class AlwaysValidInterfaceValidator : IFirewallRuleInterfaceValidator
    {
        public IResponsePayload? Validate(FirewallRuleSpecification rule) => null;
    }

    private sealed class UnsupportedMutationService : IFirewallMutationService
    {
        public ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
