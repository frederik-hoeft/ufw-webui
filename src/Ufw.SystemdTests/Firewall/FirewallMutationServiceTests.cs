using System.Collections.Immutable;
using System.Security.Cryptography;
using Moq;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Domain.Rules;
using Ufw.Ipc.Shared.Model.Requests.Domain;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Model.Responses.Domain;
using Ufw.Ipc.Shared.Security.Intent;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Systemd.Configuration;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Interop.IO;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class FirewallMutationServiceTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ListAsync_ReturnsParsedRulesWithStableIdsAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.TWO_RULES);

        RuleListResponse response = (RuleListResponse)await harness.Service.ListAsync(TestContext.CancellationToken);

        Assert.IsTrue(response.Active);
        Assert.AreEqual(2, response.Rules.Count);
        Assert.IsTrue(response.Rules[0].Parsed);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.Rules[0].RuleId));
        Assert.AreEqual(1, response.Rules[0].DisplayNumber);
        Assert.AreEqual("22", response.Rules[0].Rule!.DestinationPorts);
    }

    [TestMethod]
    public async Task AddAsync_ExecutesValidatedArgumentsAndRejectsDuplicatesAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        AddRuleRequest request = harness.SignAdd(CreateSshRule());

        RuleMutationResponse added = (RuleMutationResponse)await harness.Service.AddAsync(request, TestContext.CancellationToken);
        Assert.AreEqual(IntentOperations.ADD_RULE, added.Operation);

        harness.SetStatus(UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        IResponsePayload duplicate = await harness.Service.AddAsync(harness.SignAdd(CreateSshRule()), TestContext.CancellationToken);
        Assert.IsInstanceOfType<ConflictResponse>(duplicate);

        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                "/usr/sbin/ufw",
                It.Is<ImmutableArray<string>>(args => args[0] == "--force" && args[1] == "allow" && !args.Contains("status")),
                It.IsAny<Out<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AddAsync_RejectsInvalidSignatureWithoutCallingUfwAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        AddRuleRequest request = harness.SignAdd(CreateSshRule()) with { Signature = "AAAA" };

        IResponsePayload response = await harness.Service.AddAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(response);
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ImmutableArray<string>>(),
                It.IsAny<Out<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task DeleteAsync_UsesFreshNumberFromCurrentListAsync()
    {
        await using FirewallHarness harness = CreateHarness(
            UfwStatusFixtures.WithRules("[ 7] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        FirewallRuleSpecification rule = CreateSshRule();
        DeleteRuleRequest request = harness.SignDelete(rule);

        RuleMutationResponse deleted = (RuleMutationResponse)await harness.Service.DeleteAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(IntentOperations.DELETE_RULE, deleted.Operation);
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                "/usr/sbin/ufw",
                It.Is<ImmutableArray<string>>(args => args.SequenceEqual(new[] { "--force", "delete", "7" })),
                It.IsAny<Out<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_RejectsMissingAndAmbiguousMatchesAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        FirewallRuleSpecification rule = CreateSshRule();

        IResponsePayload missing = await harness.Service.DeleteAsync(harness.SignDelete(rule), TestContext.CancellationToken);
        Assert.IsInstanceOfType<NotFoundResponse>(missing);

        harness.SetStatus(UfwStatusFixtures.DUPLICATE_RULES);
        IResponsePayload ambiguous = await harness.Service.DeleteAsync(harness.SignDelete(rule), TestContext.CancellationToken);
        Assert.IsInstanceOfType<ConflictResponse>(ambiguous);
    }

    [TestMethod]
    public async Task AddAsync_SerializesConcurrentMutationsAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        int inFlight = 0;
        int maxInFlight = 0;
        harness.ProcessRunner
            .Setup(static runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ImmutableArray<string>>(),
                It.IsAny<Out<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string _, ImmutableArray<string> args, Out<string> output, CancellationToken cancellationToken) =>
            {
                int current = Interlocked.Increment(ref inFlight);
                int snapshot = current;
                int previousMax;
                do
                {
                    previousMax = maxInFlight;
                }
                while (snapshot > previousMax && Interlocked.CompareExchange(ref maxInFlight, snapshot, previousMax) != previousMax);

                try
                {
                    await Task.Delay(25, cancellationToken);
                    output.SetValue(args.Contains("status") ? UfwStatusFixtures.EMPTY_ACTIVE : "Rule added");
                    return 0;
                }
                finally
                {
                    Interlocked.Decrement(ref inFlight);
                }
            });

        Task<IResponsePayload> first = harness.Service.AddAsync(harness.SignAdd(CreateSshRule()), TestContext.CancellationToken).AsTask();
        Task<IResponsePayload> second = harness.Service.AddAsync(harness.SignAdd(CreateHttpRule()), TestContext.CancellationToken).AsTask();
        IResponsePayload[] results = await Task.WhenAll(first, second);

        Assert.AreEqual(1, maxInFlight);
        Assert.IsInstanceOfType<RuleMutationResponse>(results[0]);
        Assert.IsInstanceOfType<RuleMutationResponse>(results[1]);
    }

    [TestMethod]
    public async Task AddAsync_ReplayIsRejectedAfterRestartOfNonceStoreAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        AddRuleRequest request = harness.SignAdd(CreateSshRule());
        IResponsePayload first = await harness.Service.AddAsync(request, TestContext.CancellationToken);
        Assert.IsInstanceOfType<RuleMutationResponse>(first);

        IResponsePayload replay = await harness.Service.AddAsync(request, TestContext.CancellationToken);
        Assert.IsInstanceOfType<ConflictResponse>(replay);
    }

    private static FirewallRuleSpecification CreateSshRule() => new()
    {
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = "22",
        Comment = "ssh",
    };

    private static FirewallRuleSpecification CreateHttpRule() => new()
    {
        Action = FirewallAction.Allow,
        Direction = FirewallDirection.In,
        Protocol = FirewallProtocol.Tcp,
        DestinationPorts = "80",
    };

    private FirewallHarness CreateHarness(string initialStatus)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-fw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string keysPath = Path.Combine(directory, "authorized_keys");
        string noncePath = Path.Combine(directory, "nonces");
        ECDsa key = IntentSigner.CreateP256();
        File.WriteAllText(keysPath, key.ExportSubjectPublicKeyInfoPem());

        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(keysPath, noncePath));
        Mock<IChildProcessRunner> processRunner = new();
        string status = initialStatus;
        processRunner
            .Setup(static runner => runner.RunAsync(
                It.IsAny<string>(),
                It.IsAny<ImmutableArray<string>>(),
                It.IsAny<Out<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, ImmutableArray<string> args, Out<string> output, CancellationToken _) =>
            {
                if (args.Contains("status"))
                {
                    output.SetValue(status);
                    return 0;
                }

                output.SetValue("Rule updated");
                return 0;
            });

        return new FirewallHarness(directory, key, clock, configuration, processRunner, () => status, value => status = value);
    }

    private sealed class FirewallHarness : IAsyncDisposable
    {
        private readonly string _directory;
        private readonly ECDsa _key;
        private readonly TestTimeProvider _clock;
        private readonly FileAuthorizedKeyStore _keys;
        private readonly FileNonceStore _nonces;
        private readonly UfwExecutionGate _gate;
        private readonly Action<string> _setStatus;

        public FirewallHarness(
            string directory,
            ECDsa key,
            TestTimeProvider clock,
            IConfiguration configuration,
            Mock<IChildProcessRunner> processRunner,
            Func<string> getStatus,
            Action<string> setStatus)
        {
            _directory = directory;
            _key = key;
            _clock = clock;
            ProcessRunner = processRunner;
            _setStatus = setStatus;
            _keys = new FileAuthorizedKeyStore(configuration, new ConsoleLogger());
            _nonces = new FileNonceStore(configuration, clock);
            _gate = new UfwExecutionGate();
            IntentVerifier verifier = new(_keys, configuration, clock, MessageJsonSerializerContext.Default);
            UfwRunner runner = new(configuration, processRunner.Object);
            Service = new FirewallMutationService(runner, verifier, _nonces, _gate, new ConsoleLogger());
            _ = getStatus;
        }

        public Mock<IChildProcessRunner> ProcessRunner { get; }

        public FirewallMutationService Service { get; }

        public void SetStatus(string status) => _setStatus(status);

        public AddRuleRequest SignAdd(FirewallRuleSpecification rule) =>
            IntentRequestFactory.CreateAddRequest(
                _key,
                new AddRulePayload { Rule = rule },
                MessageJsonSerializerContext.Default.AddRulePayload,
                _clock);

        public DeleteRuleRequest SignDelete(FirewallRuleSpecification rule) =>
            IntentRequestFactory.CreateDeleteRequest(
                _key,
                new DeleteRulePayload { RuleId = RuleIdentity.Compute(rule), Rule = rule },
                MessageJsonSerializerContext.Default.DeleteRulePayload,
                _clock);

        public ValueTask DisposeAsync()
        {
            _gate.Dispose();
            _nonces.Dispose();
            _keys.Dispose();
            _key.Dispose();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
