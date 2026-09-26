using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Ufw.Ipc.Client;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Configuration;
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
using Ufw.Systemd.Interop.Configuration;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;
using DaemonConfiguration = Ufw.Systemd.Configuration.IConfiguration;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
[DoNotParallelize]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected arrays are local one-shot test assertions.")]
public sealed class BatchDeleteExecutionIntegrationTests : IpcProtocolTestBase
{
    private string _temporaryDirectory = null!;
    private string _mockStatePath = null!;
    private string _authorizedKeysPath = null!;
    private ECDsa _signingKey = null!;
    private MockBackedUfwRunner _runner = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), nameof(BatchDeleteExecutionIntegrationTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _mockStatePath = Path.Combine(_temporaryDirectory, "ufw-state.json");
        _authorizedKeysPath = Path.Combine(_temporaryDirectory, "authorized_keys");
        _signingKey = IntentSigner.CreateP256();
        File.WriteAllText(_authorizedKeysPath, _signingKey.ExportSubjectPublicKeyInfoPem());
        _runner = new MockBackedUfwRunner(_mockStatePath);
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

        AppSettings settings = TestAppSettingsFactory.Create();
        settings.Security = new SecurityOptions
        {
            AuthorizedKeysPath = _authorizedKeysPath,
            NonceStorePath = Path.Combine(_temporaryDirectory, "intent-nonces"),
            DeploymentIdPath = Path.Combine(_temporaryDirectory, "deployment-id"),
            ReorderRecoveryJournalPath = Path.Combine(_temporaryDirectory, "reorder-recovery.json"),
            MaxIntentAge = TimeSpan.FromMinutes(5),
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        services.RemoveAll<DaemonConfiguration>();
        services.RemoveAll<IApiEndpointMap<IRequestMessage, IResponseMessage>>();
        services.AddSingleton<DaemonConfiguration>(new TestConfiguration(settings));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IUfwRunner>(_runner);
        services.AddSingleton<IUfwRuleCommandRenderer, UfwRuleCommandRenderer>();
        services.AddSingleton<IAuthorizedKeyStore, FileAuthorizedKeyStore>();
        services.AddSingleton<INonceStore, FileNonceStore>();
        services.AddSingleton<IDeploymentIdentityProvider, FileDeploymentIdentityProvider>();
        services.AddSingleton<IIntentVerifier, IntentVerifier>();
        services.AddSingleton<IUfwExecutionGate, UfwExecutionGate>();
        services.AddSingleton<IRuleReorderPlanner, RuleReorderPlanner>();
        services.AddSingleton<IRuleReinsertabilityClassifier, RuleReinsertabilityClassifier>();
        services.AddSingleton<IReorderRecoveryJournal, FileReorderRecoveryJournal>();
        services.AddSingleton<IUfwDefaultsReader, StaticUfwDefaultsReader>();
        services.AddSingleton<IFirewallRuleSnapshotReader, FirewallRuleSnapshotReader>();
        services.AddSingleton<IRuleReorderRecoveryCoordinator, RuleReorderRecoveryCoordinator>();
        services.AddSingleton<IFirewallMutationSafetyGuard, FirewallMutationSafetyGuard>();
        services.AddSingleton<IFirewallBatchDeleteExecutor, FirewallBatchDeleteExecutor>();
        services.AddSingleton<IFirewallBatchDeleteService, FirewallBatchDeleteService>();
        services.AddSingleton<IFirewallReorderExecutor, FirewallReorderExecutor>();
        services.AddSingleton<IFirewallReorderService, FirewallReorderService>();
        services.AddSingleton<IFirewallRuleInterfaceValidator, AlwaysValidInterfaceValidator>();
        services.AddSingleton<IFirewallRuleCapabilityValidator, FirewallRuleCapabilityValidator>();
        services.AddSingleton<IFirewallOrderedInsertionExecutor, FirewallOrderedInsertionExecutor>();
        services.AddSingleton<IFirewallOrderedInsertionService, FirewallOrderedInsertionService>();
        services.AddSingleton<IFirewallRuleQueryService, FirewallRuleQueryService>();
        services.AddSingleton<IFirewallMutationService, UnsupportedMutationService>();
        services.AddScoped<IntentController>();
        services.AddScoped<RulesController>();
        services.AddSingleton<IApiEndpointMap<IRequestMessage, IResponseMessage>, UfwApiEndpointMap>();
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task SignedBatchDelete_TraversesProductionIpcAndDaemonAndRejectsReplayAsync()
    {
        await SeedIpv4RulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            BatchDeleteRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, [0, 2]);

            RuleBatchDeleteResponse response = await context.Client.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken);

            Assert.AreEqual(RuleBatchDeleteOutcome.Completed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.HasCount(2, response.Operations);
            Assert.IsTrue(response.Operations.All(static operation => operation.Outcome == RuleBatchDeleteOperationOutcome.Deleted));
            CollectionAssert.AreEqual(new[] { 2, 0 }, response.Operations.Select(static operation => operation.OccurrenceId).ToArray());
            Assert.IsEmpty(response.PendingOccurrenceIds);
            Assert.HasCount(1, response.FinalSnapshot.Rules);
            Assert.IsTrue(FirewallRuleSemanticComparer.Equals(baseline.Rules[1], response.FinalSnapshot.Rules[0]));

            UfwIpcException replay = await Assert.ThrowsExactlyAsync<UfwIpcException>(async () =>
                await context.Client.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken));
            Assert.AreEqual(409, replay.StatusCode);

            RuleListResponse afterReplay = await GetRulesAsync(context, cancellationToken);
            Assert.HasCount(1, afterReplay.Rules);
            Assert.IsTrue(FirewallRuleSemanticComparer.Equals(baseline.Rules[1], afterReplay.Rules[0]));
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SignedBatchDelete_DeletesOccurrencesAcrossAddressFamiliesAsync()
    {
        await SeedDualFamilyRulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            int ipv4Occurrence = baseline.Rules.Select((rule, index) => (rule, index)).First(static item => item.rule.Rule?.AddressFamily == FirewallAddressFamily.IPv4).index;
            int ipv6Occurrence = baseline.Rules.Select((rule, index) => (rule, index)).First(static item => item.rule.Rule?.AddressFamily == FirewallAddressFamily.IPv6).index;
            BatchDeleteRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, [ipv4Occurrence, ipv6Occurrence]);

            RuleBatchDeleteResponse response = await context.Client.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken);

            Assert.AreEqual(RuleBatchDeleteOutcome.Completed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.HasCount(4, response.FinalSnapshot.Rules);
            Assert.HasCount(2, response.Operations);
            Assert.IsFalse(response.FinalSnapshot.Rules.Any(candidate => FirewallRuleSemanticComparer.Equals(candidate, baseline.Rules[ipv4Occurrence])));
            Assert.IsFalse(response.FinalSnapshot.Rules.Any(candidate => FirewallRuleSemanticComparer.Equals(candidate, baseline.Rules[ipv6Occurrence])));
            Assert.IsTrue(response.FinalSnapshot.Rules.Any(static candidate => candidate.Rule?.AddressFamily == FirewallAddressFamily.IPv4));
            Assert.IsTrue(response.FinalSnapshot.Rules.Any(static candidate => candidate.Rule?.AddressFamily == FirewallAddressFamily.IPv6));
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task StaleBaseline_ReturnsAuthoritativeSnapshotWithoutMutationAsync()
    {
        await SeedIpv4RulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            BatchDeleteRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, [0, 2]);

            MockCommandResult external = await MockBackedUfwRunner.InvokeAsync(
                _mockStatePath,
                ["allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "53", "proto", "udp"],
                cancellationToken);
            Assert.AreEqual(0, external.ExitCode);

            RuleBatchDeleteResponse response = await context.Client.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken);

            Assert.AreEqual(RuleBatchDeleteOutcome.StaleBaseline, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.HasCount(4, response.FinalSnapshot.Rules);
            Assert.IsEmpty(response.Operations);
            CollectionAssert.AreEqual(new[] { 0, 2 }, response.PendingOccurrenceIds.ToArray());
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task MidBatchOutOfBandChange_StopsAfterConfirmedDeletionAndReportsKnownPartialStateAsync()
    {
        await SeedIpv4RulesAsync();
        bool firstDeleteSeen = false;
        bool injected = false;
        _runner.AfterCommandAsync = async (arguments, cancellationToken) =>
        {
            if (arguments.Contains("delete", StringComparer.Ordinal))
            {
                firstDeleteSeen = true;
                return;
            }
            if (!firstDeleteSeen || injected || !IsStatusCommand(arguments))
            {
                return;
            }

            injected = true;
            MockCommandResult external = await MockBackedUfwRunner.InvokeAsync(
                _mockStatePath,
                ["allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "53", "proto", "udp"],
                cancellationToken);
            Assert.AreEqual(0, external.ExitCode);
        };

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            BatchDeleteRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, [0, 2]);

            RuleBatchDeleteResponse response = await context.Client.SendAsync<BatchDeleteRulesRequest, RuleBatchDeleteResponse>(request, cancellationToken);

            Assert.IsTrue(injected);
            Assert.AreEqual(RuleBatchDeleteOutcome.PartiallyCompleted, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.HasCount(1, response.Operations);
            Assert.AreEqual(2, response.Operations[0].OccurrenceId);
            Assert.AreEqual(RuleBatchDeleteOperationOutcome.Deleted, response.Operations[0].Outcome);
            CollectionAssert.AreEqual(new[] { 0 }, response.PendingOccurrenceIds.ToArray());
            Assert.HasCount(3, response.FinalSnapshot.Rules);
            Assert.IsFalse(response.FinalSnapshot.Rules.Any(candidate => FirewallRuleSemanticComparer.Equals(candidate, baseline.Rules[2])));
            Assert.IsTrue(response.FinalSnapshot.Rules.Any(static candidate => candidate.Rule?.DestinationPorts == "53"));
        }, cancellationToken: TestContext.CancellationToken);
    }

    private BatchDeleteRulesRequest CreateSignedRequest(string deploymentId, RuleListResponse baseline, int[] occurrenceIds)
    {
        BatchDeleteRulesPayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline),
            OccurrenceIds = occurrenceIds,
        };
        return IntentRequestFactory.CreateBatchDeleteRequest(_signingKey, deploymentId, payload, MessageJsonSerializerContext.Default.BatchDeleteRulesPayload, TimeProvider.System);
    }

    private async Task SeedIpv4RulesAsync()
    {
        await AddRuleAsync("80", "allow", "tcp", "0.0.0.0/0");
        await AddRuleAsync("22", "deny", "tcp", "0.0.0.0/0");
        await AddRuleAsync("443", "allow", "tcp", "0.0.0.0/0");
        MockCommandResult enabled = await MockBackedUfwRunner.InvokeAsync(_mockStatePath, ["--force", "enable"], TestContext.CancellationToken);
        Assert.AreEqual(0, enabled.ExitCode);
    }

    private async Task SeedDualFamilyRulesAsync()
    {
        await AddRuleAsync("80", "allow", "tcp", "0.0.0.0/0");
        await AddRuleAsync("22", "deny", "tcp", "0.0.0.0/0");
        await AddRuleAsync("443", "allow", "tcp", "0.0.0.0/0");
        await AddRuleAsync("80", "allow", "tcp", "::/0");
        await AddRuleAsync("22", "deny", "tcp", "::/0");
        await AddRuleAsync("443", "allow", "tcp", "::/0");
        MockCommandResult enabled = await MockBackedUfwRunner.InvokeAsync(_mockStatePath, ["--force", "enable"], TestContext.CancellationToken);
        Assert.AreEqual(0, enabled.ExitCode);
    }

    private async Task AddRuleAsync(string port, string action, string protocol, string address)
    {
        MockCommandResult result = await MockBackedUfwRunner.InvokeAsync(
            _mockStatePath,
            [action, "from", address, "to", address, "port", port, "proto", protocol],
            TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode);
    }

    private static bool IsStatusCommand(ImmutableArray<string> arguments) =>
        arguments.Contains("status", StringComparer.Ordinal) && arguments.Contains("numbered", StringComparer.Ordinal);

    private static Task<RuleListResponse> GetRulesAsync(IIpcTestContext context, CancellationToken cancellationToken) =>
        context.Client.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);

    private static Task<IntentContextResponse> GetIntentContextAsync(IIpcTestContext context, CancellationToken cancellationToken) =>
        context.Client.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", cancellationToken);

    private sealed class AlwaysValidInterfaceValidator : IFirewallRuleInterfaceValidator
    {
        public IResponsePayload? Validate(FirewallRuleSpecification rule) => null;
    }

    private sealed class UnsupportedMutationService : IFirewallMutationService
    {
        public ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
