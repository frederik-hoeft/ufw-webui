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
    private static readonly string[] s_deleteIpv6Args = ["--force", "delete", "4"];
    private static readonly string[] s_deleteReorderedArgs = ["--force", "delete", "7"];

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ListAsync_ReturnsParsedRulesWithStableIdsAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.TWO_RULES);

        RuleListResponse response = (RuleListResponse)await harness.Service.ListAsync(TestContext.CancellationToken);

        Assert.IsTrue(response.Active);
        Assert.HasCount(2, response.Rules);
        Assert.IsTrue(response.Rules[0].Parsed);
        Assert.IsFalse(string.IsNullOrWhiteSpace(response.Rules[0].RuleId));
        Assert.AreEqual(1, response.Rules[0].DisplayNumber);
        Assert.AreEqual("22", response.Rules[0].Rule!.DestinationPorts);
    }

    [TestMethod]
    public async Task AddAsync_ExecutesValidatedArgumentsAndRejectsDuplicatesAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.SetStatusAfterNextMutation(
            UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        AddRuleRequest request = harness.SignAdd(CreateSshRule());

        RuleMutationResponse added = (RuleMutationResponse)await harness.Service.AddAsync(request, TestContext.CancellationToken);
        Assert.AreEqual(IntentOperations.ADD_RULE, added.Operation);
        Assert.IsNotNull(added.Rule);

        IResponsePayload duplicate = await harness.Service.AddAsync(harness.SignAdd(CreateSshRule()), TestContext.CancellationToken);
        Assert.IsInstanceOfType<ConflictResponse>(duplicate);

        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request =>
                    request.Command == "/usr/sbin/ufw"
                    && request.Arguments[0] == "--force"
                    && request.Arguments[1] == "allow"
                    && !request.Arguments.Contains("status")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ListAndDeleteAsync_SupportConcreteIpv6RulesAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.IPV6_RULE);

        RuleListResponse listed = (RuleListResponse)await harness.Service.ListAsync(TestContext.CancellationToken);
        Assert.HasCount(1, listed.Rules);
        Assert.IsTrue(listed.Rules[0].Parsed);
        FirewallRuleSpecification listedRule = listed.Rules[0].Rule!;
        Assert.AreEqual(FirewallAddressFamily.IPv6, listedRule.AddressFamily);
        harness.SetStatusAfterNextMutation(UfwStatusFixtures.EMPTY_ACTIVE);

        RuleMutationResponse deleted = (RuleMutationResponse)await harness.Service.DeleteAsync(
            harness.SignDelete(listedRule),
            TestContext.CancellationToken);
        Assert.AreEqual(IntentOperations.DELETE_RULE, deleted.Operation);
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request => request.Arguments.SequenceEqual(s_deleteIpv6Args)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AddAsync_FamilyNeutralRuleRejectsExistingConcreteIpv6DuplicateAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.IPV6_RULE);

        IResponsePayload response = await harness.Service.AddAsync(
            harness.SignAdd(CreateSshRule()),
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<ConflictResponse>(response);
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request => !request.Arguments.Contains("status")),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task AddAsync_RejectsInvalidSignatureWithoutCallingUfwAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        AddRuleRequest request = harness.SignAdd(CreateSshRule()) with { Signature = "AAAA" };

        IResponsePayload response = await harness.Service.AddAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(response);
        VerifyNoUfwCalls(harness);
    }

    [TestMethod]
    public async Task AddAsync_RejectsWrongDeploymentWithoutCallingUfwAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        AddRuleRequest request = harness.SignAdd(CreateSshRule(), deploymentId: "another-deployment");

        IResponsePayload response = await harness.Service.AddAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(response);
        VerifyNoUfwCalls(harness);
    }

    [TestMethod]
    public async Task AddAsync_RejectsMalformedPayloadWithoutCallingUfwAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        AddRuleRequest request = harness.SignAdd(CreateSshRule()) with
        {
            Payload = System.Text.Json.JsonSerializer.SerializeToElement(new { rule = "malformed" })
        };

        IResponsePayload response = await harness.Service.AddAsync(request, TestContext.CancellationToken);

        Assert.IsInstanceOfType<BadRequestResponse>(response);
        VerifyNoUfwCalls(harness);
    }

    [TestMethod]
    public async Task AddAsync_ReplayPersistenceFailureDoesNotCallUfwAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.BreakNoncePersistence();

        await Assert.ThrowsAsync<IOException>(async () =>
            _ = await harness.Service.AddAsync(harness.SignAdd(CreateSshRule()), TestContext.CancellationToken));

        VerifyNoUfwCalls(harness);
    }

    [TestMethod]
    public async Task DeleteAsync_UsesFreshNumberFromCurrentListAsync()
    {
        await using FirewallHarness harness = CreateHarness(
            UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        RuleListResponse listed = (RuleListResponse)await harness.Service.ListAsync(TestContext.CancellationToken);
        FirewallRuleSpecification rule = listed.Rules[0].Rule!;
        DeleteRuleRequest request = harness.SignDelete(rule);

        harness.SetStatus(
            UfwStatusFixtures.WithRules("[ 7] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        harness.SetStatusAfterNextMutation(UfwStatusFixtures.EMPTY_ACTIVE);

        RuleMutationResponse deleted = (RuleMutationResponse)await harness.Service.DeleteAsync(request, TestContext.CancellationToken);

        Assert.AreEqual(IntentOperations.DELETE_RULE, deleted.Operation);
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request => request.Arguments.SequenceEqual(s_deleteReorderedArgs)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task DeleteAsync_RejectsMissingAndAmbiguousMatchesAsync()
    {
        await using FirewallHarness harness = CreateHarness(
            UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere"));
        RuleListResponse listed = (RuleListResponse)await harness.Service.ListAsync(TestContext.CancellationToken);
        FirewallRuleSpecification rule = listed.Rules[0].Rule!;

        harness.SetStatus(UfwStatusFixtures.EMPTY_ACTIVE);
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
        HashSet<string> addedPorts = new(StringComparer.Ordinal);
        harness.SetProcessHandler(async (request, cancellationToken) =>
        {
            if (request.Arguments.Contains("status"))
            {
                return new ChildProcessResult(0, harness.CurrentStatus, string.Empty, CancellationRequested: false);
            }

            int current = Interlocked.Increment(ref inFlight);
            UpdateMaximum(ref maxInFlight, current);
            try
            {
                await Task.Delay(25, cancellationToken);
                string port = GetDestinationPort(request.Arguments);
                addedPorts.Add(port);
                harness.SetStatus(StatusForPorts(addedPorts));
                return new ChildProcessResult(0, "Rule added\n", string.Empty, CancellationRequested: false);
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
    public async Task AddAsync_ConcurrentReplayCrossesMutationBoundaryAtMostOnceAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.SetStatusAfterNextMutation(
            UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        AddRuleRequest request = harness.SignAdd(CreateSshRule());

        IResponsePayload[] results = await Task.WhenAll(
            harness.Service.AddAsync(request, TestContext.CancellationToken).AsTask(),
            harness.Service.AddAsync(request, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(1, results.Count(static result => result is RuleMutationResponse));
        Assert.AreEqual(1, results.Count(static result => result is ConflictResponse));
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request => !request.Arguments.Contains("status")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AddAsync_ReplayIsRejectedAfterNonceStoreRestartAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.SetStatusAfterNextMutation(
            UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh"));
        AddRuleRequest request = harness.SignAdd(CreateSshRule());
        IResponsePayload first = await harness.Service.AddAsync(request, TestContext.CancellationToken);
        Assert.IsInstanceOfType<RuleMutationResponse>(first);

        harness.RestartNonceStore();

        IResponsePayload replay = await harness.Service.AddAsync(request, TestContext.CancellationToken);
        Assert.IsInstanceOfType<ConflictResponse>(replay);
    }

    [TestMethod]
    public async Task AddAsync_SuccessfulExitWithoutObservedRuleFailsReconciliationAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);

        IResponsePayload response = await harness.Service.AddAsync(
            harness.SignAdd(CreateSshRule()),
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<InternalServerErrorResponse>(response);
    }

    [TestMethod]
    public async Task DeleteAsync_SuccessfulExitWhileRuleRemainsFailsReconciliationAsync()
    {
        await using FirewallHarness harness = CreateHarness(
            UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere"));
        RuleListResponse listed = (RuleListResponse)await harness.Service.ListAsync(TestContext.CancellationToken);

        IResponsePayload response = await harness.Service.DeleteAsync(
            harness.SignDelete(listed.Rules[0].Rule!),
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<InternalServerErrorResponse>(response);
    }

    [TestMethod]
    public async Task AddAsync_ProcessStartFailureReturnsInternalErrorAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.ProcessRunner
            .Setup(static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request => !request.Arguments.Contains("status")),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ChildProcessException("start failed", new IOException("test")));

        IResponsePayload response = await harness.Service.AddAsync(
            harness.SignAdd(CreateSshRule()),
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<InternalServerErrorResponse>(response);
    }

    [TestMethod]
    public async Task AddAsync_NonzeroExitReturnsUnprocessableWithoutClaimingSuccessAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.ProcessRunner
            .Setup(static runner => runner.RunAsync(
                It.Is<ChildProcessRequest>(request => !request.Arguments.Contains("status")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(1, string.Empty, "bad rule\n", CancellationRequested: false));

        IResponsePayload response = await harness.Service.AddAsync(
            harness.SignAdd(CreateSshRule()),
            TestContext.CancellationToken);

        Assert.IsInstanceOfType<UnprocessableContentResponse>(response);
    }

    [TestMethod]
    public async Task ListAsync_ParsesStdoutAndDoesNotTreatStderrAsRuleDataAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.ProcessRunner.Reset();
        harness.ProcessRunner
            .Setup(static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(0, UfwStatusFixtures.EMPTY_ACTIVE, "diagnostic garbage\n", CancellationRequested: false));

        IResponsePayload response = await harness.Service.ListAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<RuleListResponse>(response);
    }

    [TestMethod]
    public async Task ListAsync_UnexpectedSuccessfulStdoutFailsSafelyAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        harness.ProcessRunner.Reset();
        harness.ProcessRunner
            .Setup(static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChildProcessResult(0, "unexpected output\n", string.Empty, CancellationRequested: false));

        IResponsePayload response = await harness.Service.ListAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<InternalServerErrorResponse>(response);
    }

    [TestMethod]
    public async Task AddAsync_CancellationAfterSuccessfulChildExitStillReconcilesBeforeReturningAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        using CancellationTokenSource mutationCancellation = new();
        int statusCalls = 0;
        harness.SetProcessHandler(async (request, _) =>
        {
            if (request.Arguments.Contains("status"))
            {
                int call = Interlocked.Increment(ref statusCalls);
                string status = call == 1
                    ? UfwStatusFixtures.EMPTY_ACTIVE
                    : UfwStatusFixtures.WithRules("[ 1] 22/tcp                     ALLOW IN    Anywhere                   # ssh");
                return new ChildProcessResult(0, status, string.Empty, CancellationRequested: false);
            }

            await mutationCancellation.CancelAsync();
            return new ChildProcessResult(0, "Rule added\n", string.Empty, CancellationRequested: false);
        });

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await harness.Service.AddAsync(harness.SignAdd(CreateSshRule()), mutationCancellation.Token));

        Assert.AreEqual(2, Volatile.Read(ref statusCalls), "The authoritative postcondition must be read even when cancellation arrives after the mutating child exits.");
    }

    [TestMethod]
    public async Task AddAsync_CancellationAfterProcessStartKeepsGateUntilReapedAndReconciledAsync()
    {
        await using FirewallHarness harness = CreateHarness(UfwStatusFixtures.EMPTY_ACTIVE);
        TaskCompletionSource mutationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseMutation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int statusCalls = 0;
        harness.SetProcessHandler(async (request, _) =>
        {
            if (request.Arguments.Contains("status"))
            {
                Interlocked.Increment(ref statusCalls);
                return new ChildProcessResult(0, harness.CurrentStatus, string.Empty, CancellationRequested: false);
            }

            mutationStarted.TrySetResult();
            await releaseMutation.Task;
            return new ChildProcessResult(137, string.Empty, "terminated\n", CancellationRequested: true);
        });

        using CancellationTokenSource mutationCancellation = new();
        Task<IResponsePayload> mutation = harness.Service.AddAsync(
            harness.SignAdd(CreateSshRule()),
            mutationCancellation.Token).AsTask();
        await mutationStarted.Task;
        await mutationCancellation.CancelAsync();

        Task<IResponsePayload> queuedRead = harness.Service.ListAsync(TestContext.CancellationToken).AsTask();
        await Task.Delay(25, TestContext.CancellationToken);
        Assert.AreEqual(1, Volatile.Read(ref statusCalls), "The queued read must not cross the execution gate while the mutation child is still owned.");

        releaseMutation.TrySetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await mutation);
        RuleListResponse listed = (RuleListResponse)await queuedRead;
        Assert.IsTrue(listed.Active);
        Assert.IsTrue(Volatile.Read(ref statusCalls) >= 3, "Cancellation reconciliation must occur before the queued request runs.");
    }

    private static void VerifyNoUfwCalls(FirewallHarness harness) =>
        harness.ProcessRunner.Verify(
            static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

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

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int previous;
        do
        {
            previous = maximum;
        }
        while (candidate > previous && Interlocked.CompareExchange(ref maximum, candidate, previous) != previous);
    }

    private static string GetDestinationPort(ImmutableArray<string> arguments)
    {
        int toIndex = arguments.IndexOf("to");
        int portIndex = arguments.IndexOf("port", toIndex + 1);
        return arguments[portIndex + 1];
    }

    private static string StatusForPorts(IEnumerable<string> ports)
    {
        List<string> rows = [];
        int number = 1;
        foreach (string port in ports.OrderBy(static port => port, StringComparer.Ordinal))
        {
            string comment = port == "22" ? "                   # ssh" : string.Empty;
            rows.Add($"[ {number}] {port}/tcp                     ALLOW IN    Anywhere{comment}");
            number++;
        }

        return UfwStatusFixtures.WithRules([.. rows]);
    }

    private FirewallHarness CreateHarness(string initialStatus)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-fw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string keysPath = Path.Combine(directory, "authorized_keys");
        string noncePath = Path.Combine(directory, "nonces");
        string deploymentPath = Path.Combine(directory, "deployment-id");
        ECDsa key = IntentSigner.CreateP256();
        File.WriteAllText(keysPath, key.ExportSubjectPublicKeyInfoPem());

        TestTimeProvider clock = new(DateTimeOffset.Parse("2026-04-01T12:00:00Z"));
        TestConfiguration configuration = new(TestAppSettingsFactory.Create(keysPath, noncePath, deploymentPath));
        Mock<IChildProcessRunner> processRunner = new();
        return new FirewallHarness(directory, key, clock, configuration, processRunner, initialStatus);
    }

    private sealed class FirewallHarness : IAsyncDisposable
    {
        private readonly string _directory;
        private readonly ECDsa _key;
        private readonly TestTimeProvider _clock;
        private readonly IConfiguration _configuration;
        private readonly FileAuthorizedKeyStore _keys;
        private readonly FileDeploymentIdentityProvider _deploymentIdentity;
        private readonly UfwExecutionGate _gate;
        private readonly string _noncePath;
        private FileNonceStore _nonces;
        private string _status;
        private string? _statusAfterNextMutation;

        public FirewallHarness(
            string directory,
            ECDsa key,
            TestTimeProvider clock,
            IConfiguration configuration,
            Mock<IChildProcessRunner> processRunner,
            string initialStatus)
        {
            _directory = directory;
            _key = key;
            _clock = clock;
            _configuration = configuration;
            _noncePath = configuration.Settings.Security!.NonceStorePath;
            ProcessRunner = processRunner;
            _status = initialStatus;
            ConfigureDefaultProcessRunner();
            _keys = new FileAuthorizedKeyStore(configuration, new ConsoleLogger());
            _deploymentIdentity = new FileDeploymentIdentityProvider(configuration);
            _nonces = new FileNonceStore(configuration, clock);
            _gate = new UfwExecutionGate();
            Service = CreateService();
        }

        public Mock<IChildProcessRunner> ProcessRunner { get; }

        public FirewallMutationService Service { get; private set; }

        public string CurrentStatus => _status;

        public void SetStatus(string status) => _status = status;

        public void SetStatusAfterNextMutation(string status) => _statusAfterNextMutation = status;

        public void SetProcessHandler(Func<ChildProcessRequest, CancellationToken, Task<ChildProcessResult>> handler)
        {
            ProcessRunner.Reset();
            ProcessRunner
                .Setup(static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()))
                .Returns(handler);
        }

        public AddRuleRequest SignAdd(FirewallRuleSpecification rule, string? deploymentId = null) =>
            IntentRequestFactory.CreateAddRequest(
                _key,
                deploymentId ?? _deploymentIdentity.GetDeploymentId(),
                new AddRulePayload { Rule = rule },
                MessageJsonSerializerContext.Default.AddRulePayload,
                _clock);

        public DeleteRuleRequest SignDelete(FirewallRuleSpecification rule) =>
            IntentRequestFactory.CreateDeleteRequest(
                _key,
                _deploymentIdentity.GetDeploymentId(),
                new DeleteRulePayload { RuleId = RuleIdentity.Compute(rule), Rule = rule },
                MessageJsonSerializerContext.Default.DeleteRulePayload,
                _clock);

        public void RestartNonceStore()
        {
            _nonces.Dispose();
            _nonces = new FileNonceStore(_configuration, _clock);
            Service = CreateService();
        }

        public void BreakNoncePersistence()
        {
            if (File.Exists(_noncePath))
            {
                File.Delete(_noncePath);
            }

            Directory.CreateDirectory(_noncePath);
        }

        private void ConfigureDefaultProcessRunner()
        {
            ProcessRunner
                .Setup(static runner => runner.RunAsync(It.IsAny<ChildProcessRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChildProcessRequest request, CancellationToken _) =>
                {
                    if (request.Arguments.Contains("status"))
                    {
                        return new ChildProcessResult(0, _status, string.Empty, CancellationRequested: false);
                    }

                    if (_statusAfterNextMutation is not null)
                    {
                        _status = _statusAfterNextMutation;
                        _statusAfterNextMutation = null;
                    }

                    return new ChildProcessResult(0, "Rule updated\n", string.Empty, CancellationRequested: false);
                });
        }

        private FirewallMutationService CreateService()
        {
            IntentVerifier verifier = new(
                _keys,
                _deploymentIdentity,
                _configuration,
                _clock,
                MessageJsonSerializerContext.Default);
            UfwRunner runner = new(_configuration, ProcessRunner.Object);
            return new FirewallMutationService(runner, verifier, _nonces, _gate, new ConsoleLogger());
        }

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
