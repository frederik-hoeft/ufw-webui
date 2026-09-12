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
using Ufw.Systemd.Firewall.Insertion;
using Ufw.Systemd.Firewall.Ordering;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;
using DaemonConfiguration = Ufw.Systemd.Configuration.IConfiguration;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
[DoNotParallelize]
public sealed class OrderedInsertionExecutionIntegrationTests : IpcProtocolTestBase
{
    private string _temporaryDirectory = null!;
    private string _mockStatePath = null!;
    private string _authorizedKeysPath = null!;
    private ECDsa _signingKey = null!;
    private MockBackedUfwRunner _runner = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), nameof(OrderedInsertionExecutionIntegrationTests), Guid.NewGuid().ToString("N"));
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
        services.AddSingleton<IFirewallRuleInterfaceValidator, AlwaysValidInterfaceValidator>();
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
    public async Task SignedInsertion_BeforeAnchorTraversesProductionIpcAndRejectsReplayAsync()
    {
        await SeedDualFamilyRulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            FirewallRuleSpecification inserted = Rule("53", FirewallAddressFamily.IPv4, FirewallAction.Allow, FirewallProtocol.Udp);
            InsertRuleRequest request = CreateSignedRequest(
                intentContext.DeploymentId,
                baseline,
                anchorOccurrenceId: 1,
                RuleInsertionPlacement.Before,
                inserted);

            RuleInsertionResponse response = await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken);

            Assert.AreEqual(RuleInsertionOutcome.Completed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsNotNull(response.InsertedRule);
            Assert.AreEqual(FirewallAddressFamily.IPv4, response.InsertedRule.Rule!.AddressFamily);
            Assert.AreEqual("53", response.InsertedRule.Rule.DestinationPorts);
            Assert.IsTrue(FirewallRuleSemanticComparer.Equals(baseline.Rules[0], response.FinalSnapshot.Rules[0]));
            Assert.IsTrue(FirewallRuleSemanticComparer.Equals(response.InsertedRule, response.FinalSnapshot.Rules[1]));
            Assert.IsTrue(FirewallRuleSemanticComparer.Equals(baseline.Rules[1], response.FinalSnapshot.Rules[2]));

            UfwIpcException replay = await Assert.ThrowsExactlyAsync<UfwIpcException>(async () =>
                await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken));
            Assert.AreEqual(409, replay.StatusCode);
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task SignedInsertion_AfterLastIpv4AnchorStaysBeforeIpv6PartitionAsync()
    {
        await SeedDualFamilyRulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            int lastIpv4Occurrence = baseline.Rules
                .Select((rule, index) => (rule, index))
                .Where(static item => item.rule.Rule?.AddressFamily == FirewallAddressFamily.IPv4)
                .Select(static item => item.index)
                .Last();
            int firstIpv6Occurrence = baseline.Rules
                .Select((rule, index) => (rule, index))
                .First(static item => item.rule.Rule?.AddressFamily == FirewallAddressFamily.IPv6)
                .index;
            FirewallRuleSpecification inserted = Rule("8443", FirewallAddressFamily.IPv4, FirewallAction.Allow, FirewallProtocol.Tcp);
            InsertRuleRequest request = CreateSignedRequest(
                intentContext.DeploymentId,
                baseline,
                lastIpv4Occurrence,
                RuleInsertionPlacement.After,
                inserted);

            RuleInsertionResponse response = await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken);

            Assert.AreEqual(RuleInsertionOutcome.Completed, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsNotNull(response.InsertedRule);
            Assert.IsTrue(FirewallRuleSemanticComparer.Equals(response.InsertedRule, response.FinalSnapshot.Rules[firstIpv6Occurrence]));
            Assert.AreEqual(FirewallAddressFamily.IPv4, response.FinalSnapshot.Rules[firstIpv6Occurrence].Rule!.AddressFamily);
            Assert.AreEqual(FirewallAddressFamily.IPv6, response.FinalSnapshot.Rules[firstIpv6Occurrence + 1].Rule!.AddressFamily);
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task StaleBaseline_ReturnsCurrentSnapshotWithoutInsertionAsync()
    {
        await SeedIpv4RulesAsync();

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            InsertRuleRequest request = CreateSignedRequest(
                intentContext.DeploymentId,
                baseline,
                anchorOccurrenceId: 0,
                RuleInsertionPlacement.Before,
                Rule("53", FirewallAddressFamily.IPv4, FirewallAction.Allow, FirewallProtocol.Udp));

            MockCommandResult external = await MockBackedUfwRunner.InvokeAsync(
                _mockStatePath,
                ["allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "123", "proto", "udp"],
                cancellationToken);
            Assert.AreEqual(0, external.ExitCode);

            RuleInsertionResponse response = await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken);

            Assert.AreEqual(RuleInsertionOutcome.StaleBaseline, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsNull(response.InsertedRule);
            Assert.HasCount(baseline.Rules.Count + 1, response.FinalSnapshot.Rules);
            Assert.IsFalse(response.FinalSnapshot.Rules.Any(static rule => rule.Rule?.DestinationPorts == "53"));
        }, cancellationToken: TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task OutOfBandChangeAfterInsert_ReturnsStateUncertainWithAuthoritativeSnapshotAsync()
    {
        await SeedIpv4RulesAsync();
        bool injected = false;
        _runner.AfterCommandAsync = async (arguments, cancellationToken) =>
        {
            if (injected || !arguments.Contains("insert", StringComparer.Ordinal))
            {
                return;
            }

            injected = true;
            MockCommandResult external = await MockBackedUfwRunner.InvokeAsync(
                _mockStatePath,
                ["allow", "from", "0.0.0.0/0", "to", "0.0.0.0/0", "port", "123", "proto", "udp"],
                cancellationToken);
            Assert.AreEqual(0, external.ExitCode);
        };

        await RunAsync(async (context, cancellationToken) =>
        {
            RuleListResponse baseline = await GetRulesAsync(context, cancellationToken);
            IntentContextResponse intentContext = await GetIntentContextAsync(context, cancellationToken);
            InsertRuleRequest request = CreateSignedRequest(
                intentContext.DeploymentId,
                baseline,
                anchorOccurrenceId: 1,
                RuleInsertionPlacement.Before,
                Rule("53", FirewallAddressFamily.IPv4, FirewallAction.Allow, FirewallProtocol.Udp));

            RuleInsertionResponse response = await context.Client.SendAsync<InsertRuleRequest, RuleInsertionResponse>(request, cancellationToken);

            Assert.IsTrue(injected);
            Assert.AreEqual(RuleInsertionOutcome.StateUncertain, response.Outcome);
            Assert.IsNotNull(response.FinalSnapshot);
            Assert.IsNull(response.InsertedRule);
            Assert.IsTrue(response.FinalSnapshot.Rules.Any(static rule => rule.Rule?.DestinationPorts == "53"));
            Assert.IsTrue(response.FinalSnapshot.Rules.Any(static rule => rule.Rule?.DestinationPorts == "123"));
        }, cancellationToken: TestContext.CancellationToken);
    }

    private InsertRuleRequest CreateSignedRequest(
        string deploymentId,
        RuleListResponse baseline,
        int anchorOccurrenceId,
        RuleInsertionPlacement placement,
        FirewallRuleSpecification rule)
    {
        InsertRulePayload payload = new()
        {
            BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(baseline),
            AnchorOccurrenceId = anchorOccurrenceId,
            Placement = placement,
            Rule = rule,
        };
        return IntentRequestFactory.CreateInsertRequest(
            _signingKey,
            deploymentId,
            payload,
            MessageJsonSerializerContext.Default.InsertRulePayload,
            TimeProvider.System);
    }

    private static Task<RuleListResponse> GetRulesAsync(
        IIpcTestContext context,
        CancellationToken cancellationToken) =>
        context.Client.SendAsync<RuleListResponse>(RequestMethod.Get, "/api/v1/rules", cancellationToken);

    private static Task<IntentContextResponse> GetIntentContextAsync(
        IIpcTestContext context,
        CancellationToken cancellationToken) =>
        context.Client.SendAsync<IntentContextResponse>(RequestMethod.Get, "/api/v1/intent/context", cancellationToken);

    private async Task SeedIpv4RulesAsync()
    {
        await AddRuleAsync("80", FirewallAddressFamily.IPv4, "allow", "tcp");
        await AddRuleAsync("22", FirewallAddressFamily.IPv4, "deny", "tcp");
        await AddRuleAsync("443", FirewallAddressFamily.IPv4, "allow", "tcp");
        await EnableAsync();
    }

    private async Task SeedDualFamilyRulesAsync()
    {
        await AddRuleAsync("80", FirewallAddressFamily.IPv4, "allow", "tcp");
        await AddRuleAsync("22", FirewallAddressFamily.IPv4, "deny", "tcp");
        await AddRuleAsync("80", FirewallAddressFamily.IPv6, "allow", "tcp");
        await AddRuleAsync("22", FirewallAddressFamily.IPv6, "deny", "tcp");
        await EnableAsync();
    }

    private async Task AddRuleAsync(string port, FirewallAddressFamily family, string action, string protocol)
    {
        string address = family == FirewallAddressFamily.IPv6 ? "::/0" : "0.0.0.0/0";
        MockCommandResult result = await MockBackedUfwRunner.InvokeAsync(
            _mockStatePath,
            [action, "from", address, "to", address, "port", port, "proto", protocol],
            TestContext.CancellationToken);
        Assert.AreEqual(0, result.ExitCode);
    }

    private async Task EnableAsync()
    {
        MockCommandResult enabled = await MockBackedUfwRunner.InvokeAsync(
            _mockStatePath,
            ["--force", "enable"],
            TestContext.CancellationToken);
        Assert.AreEqual(0, enabled.ExitCode);
    }

    private static FirewallRuleSpecification Rule(
        string port,
        FirewallAddressFamily family,
        FirewallAction action,
        FirewallProtocol protocol) => new()
        {
            Action = action,
            AddressFamily = family,
            Direction = FirewallDirection.In,
            Protocol = protocol,
            DestinationPorts = port,
        };

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
