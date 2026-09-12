using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;
using DaemonConfiguration = Ufw.Systemd.Configuration.IConfiguration;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
[DoNotParallelize]
public sealed class ReorderExecutionIntegrationTests : IpcProtocolTestBase
{
    private string _temporaryDirectory = null!;
    private string _mockStatePath = null!;
    private string _authorizedKeysPath = null!;
    private ECDsa _signingKey = null!;
    private MockBackedUfwRunner _runner = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), nameof(ReorderExecutionIntegrationTests), Guid.NewGuid().ToString("N"));
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
        services.AddSingleton<IFirewallRuleSnapshotReader, FirewallRuleSnapshotReader>();
        services.AddSingleton<IRuleReorderRecoveryCoordinator, RuleReorderRecoveryCoordinator>();
        services.AddSingleton<IFirewallMutationSafetyGuard, FirewallMutationSafetyGuard>();
        services.AddSingleton<IFirewallReorderExecutor, FirewallReorderExecutor>();
        services.AddSingleton<IFirewallReorderService, FirewallReorderService>();
        services.AddSingleton<IFirewallRuleQueryService, FirewallRuleQueryService>();
        services.AddSingleton<IFirewallMutationService, UnsupportedMutationService>();
        services.AddScoped<IntentController>();
        services.AddScoped<RulesController>();
        services.AddSingleton<IApiEndpointMap<IRequestMessage, IResponseMessage>, UfwApiEndpointMap>();
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public async Task SignedReorder_TraversesProductionIpcAndDaemonAgainstUfwMockAsync()
    {
        await SeedDualFamilyRulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await context.Client.SendAsync<RuleListResponse>(
                RequestMethod.Get,
                "/api/v1/rules",
                cancellationToken);
            IntentContextResponse intentContext = await context.Client.SendAsync<IntentContextResponse>(
                RequestMethod.Get,
                "/api/v1/intent/context",
                cancellationToken);

            Assert.HasCount(6, baseline.Rules);
            int[] desiredOrder = [2, 0, 1, 5, 3, 4];
            ReorderRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, desiredOrder);

            RuleReorderResponse response = await context.Client.SendAsync<ReorderRulesRequest, RuleReorderResponse>(
                request,
                cancellationToken);

            Assert.AreEqual(RuleReorderOutcome.Completed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsEmpty(response.BlockedOperations);
            Assert.IsEmpty(response.PendingOperations);
            CollectionAssert.AreEqual(desiredOrder, MapToBaselineOccurrences(baseline, response.FinalSnapshot));
            Assert.IsGreaterThan(0, response.Operations.Length);

            UfwIpcException replay = await Assert.ThrowsExactlyAsync<UfwIpcException>(async () =>
                await context.Client.SendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken));
            Assert.AreEqual(409, replay.StatusCode);

            RuleListResponse afterReplay = await context.Client.SendAsync<RuleListResponse>(
                RequestMethod.Get,
                "/api/v1/rules",
                cancellationToken);
            CollectionAssert.AreEqual(desiredOrder, MapToBaselineOccurrences(baseline, afterReplay));
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task StaleBaseline_ReturnsAuthoritativeSnapshotWithoutMutationAsync()
    {
        await SeedIpv4RulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await context.Client.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);
            IntentContextResponse intentContext = await context.Client.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", cancellationToken);
            ReorderRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, [2, 0, 1]);

            MockCommandResult external = await MockBackedUfwRunner.InvokeAsync(
                _mockStatePath,
                ["allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "53", "proto", "udp"],
                cancellationToken);
            Assert.AreEqual(0, external.ExitCode);

            RuleReorderResponse response = await context.Client.SendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken);

            Assert.AreEqual(RuleReorderOutcome.StaleBaseline, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.HasCount(4, response.FinalSnapshot.Rules);
            Assert.IsEmpty(response.Operations);
            Assert.IsEmpty(response.BlockedOperations);
            Assert.IsEmpty(response.PendingOperations);
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task MidMoveOutOfBandChange_RecoversDeletedRuleAndReportsPartialExecutionAsync()
    {
        await SeedIpv4RulesAsync();
        bool injected = false;
        _runner.AfterCommandAsync = async (arguments, cancellationToken) =>
        {
            if (injected || !arguments.Contains("delete", StringComparer.Ordinal))
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
            RuleListResponse baseline = await context.Client.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);
            IntentContextResponse intentContext = await context.Client.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", cancellationToken);
            ReorderRulesRequest request = CreateSignedRequest(intentContext.DeploymentId, baseline, [2, 0, 1]);

            RuleReorderResponse response = await context.Client.SendAsync<ReorderRulesRequest, RuleReorderResponse>(request, cancellationToken);

            Assert.IsTrue(injected);
            Assert.AreEqual(RuleReorderOutcome.PartiallyCompleted, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.HasCount(4, response.FinalSnapshot.Rules);
            Assert.HasCount(1, response.Operations);
            Assert.AreEqual(RuleReorderOperationOutcome.FailedAndRestored, response.Operations[0].Outcome);
            Assert.HasCount(1, response.BlockedOperations);
            Assert.IsEmpty(response.PendingOperations);

            foreach (ListedFirewallRule original in baseline.Rules)
            {
                Assert.IsTrue(response.FinalSnapshot.Rules.Any(candidate => SameRule(candidate, original)));
            }
        }, cancellationToken: TestContext.CancellationToken);
    }

    private ReorderRulesRequest CreateSignedRequest(string deploymentId, RuleListResponse baseline, int[] desiredOrder)
    {
        ReorderRulesPayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline),
            DesiredOrder = desiredOrder,
        };
        return IntentRequestFactory.CreateReorderRequest(
            _signingKey,
            deploymentId,
            payload,
            MessageJsonSerializerContext.Default.ReorderRulesPayload,
            TimeProvider.System);
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

    private static int[] MapToBaselineOccurrences(RuleListResponse baseline, RuleListResponse current)
    {
        bool[] used = new bool[baseline.Rules.Count];
        int[] result = new int[current.Rules.Count];
        for (int currentIndex = 0; currentIndex < current.Rules.Count; currentIndex++)
        {
            int match = -1;
            for (int baselineIndex = 0; baselineIndex < baseline.Rules.Count; baselineIndex++)
            {
                if (used[baselineIndex] || !SameRule(current.Rules[currentIndex], baseline.Rules[baselineIndex]))
                {
                    continue;
                }
                match = baselineIndex;
                break;
            }
            Assert.IsGreaterThanOrEqualTo(0, match);
            used[match] = true;
            result[currentIndex] = match;
        }
        return result;
    }

    private static bool SameRule(ListedFirewallRule left, ListedFirewallRule right) =>
        FirewallRuleSemanticComparer.Equals(left, right);

    private sealed class UnsupportedMutationService : IFirewallMutationService
    {
        public ValueTask<IResponsePayload> AddAsync(AddRuleRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IResponsePayload> DeleteAsync(DeleteRuleRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
