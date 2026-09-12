using Moq;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Requests.Domain;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Shared.Ipc.Model.Responses.Domain;
using Ufw.Shared.Security.Intent;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Insertion;
using Ufw.Systemd.Security.Intent;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Firewall.Insertion;

[TestClass]
[SuppressMessage("Performance", "CA1861:Prefer 'static readonly' fields over constant array arguments", Justification = "Expected call order is a local one-shot assertion.")]
public sealed class FirewallOrderedInsertionServiceTests
{
    private const string NONCE = "test-nonce";
    private const long EXPIRES_AT_UNIX = 2_000_000_000;

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task InsertAsync_HoldsSharedGateAndRunsSafetyBeforeNonceAndExecutorAsync()
    {
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        Mock<IFirewallOrderedInsertionExecutor> executor = new(MockBehavior.Strict);
        List<string> calls = [];
        gate.Setup(value => value.RunAsync(It.IsAny<Func<CancellationToken, Task<IResponsePayload>>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<IResponsePayload>>, CancellationToken>(async (action, cancellationToken) =>
            {
                calls.Add("gate");
                return await action(cancellationToken);
            });
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(_ =>
            {
                calls.Add("guard");
                return Task.CompletedTask;
            });
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .Returns<string, long, CancellationToken>((_, _, _) =>
            {
                calls.Add("nonce");
                return ValueTask.FromResult(true);
            });
        executor.Setup(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()))
            .Returns<InsertRulePayload, CancellationToken>((_, _) =>
            {
                calls.Add("executor");
                return Task.FromResult(CompletedResult());
            });
        FirewallOrderedInsertionService service = new(
            verifier.Object, nonceStore.Object, gate.Object, guard.Object, executor.Object);

        IResponsePayload result = await service.InsertAsync(CreateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<RuleInsertionResponse>(result);
        CollectionAssert.AreEqual(new[] { "gate", "guard", "nonce", "executor" }, calls);
    }

    [TestMethod]
    public async Task InsertAsync_RejectedIntentDoesNotConsumeNonceOrEnterMutationBoundaryAsync()
    {
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier.Setup(value => value.VerifyInsert(It.IsAny<InsertRuleRequest>()))
            .Returns(new IntentVerificationResult.Rejected(new ForbiddenResponse("invalid signature")));
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        Mock<IFirewallOrderedInsertionExecutor> executor = new(MockBehavior.Strict);
        FirewallOrderedInsertionService service = new(
            verifier.Object, nonceStore.Object, gate.Object, guard.Object, executor.Object);

        IResponsePayload result = await service.InsertAsync(CreateRequest(), TestContext.CancellationToken);

        Assert.IsInstanceOfType<ForbiddenResponse>(result);
        nonceStore.VerifyNoOtherCalls();
        gate.VerifyNoOtherCalls();
        guard.VerifyNoOtherCalls();
        executor.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task InsertAsync_ReplayIsRejectedBeforeExecutorRunsAgainAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.SetupSequence(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
        Mock<IFirewallOrderedInsertionExecutor> executor = CreateExecutor();
        FirewallOrderedInsertionService service = new(
            verifier.Object, nonceStore.Object, gate, guard.Object, executor.Object);
        InsertRuleRequest request = CreateRequest();

        Assert.IsInstanceOfType<RuleInsertionResponse>(await service.InsertAsync(request, TestContext.CancellationToken));
        Assert.IsInstanceOfType<ConflictResponse>(await service.InsertAsync(request, TestContext.CancellationToken));
        executor.Verify(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InsertAsync_ConcurrentReplayCrossesExecutionBoundaryAtMostOnceAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        int consumed = 0;
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interlocked.Increment(ref consumed) == 1);
        Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
        Mock<IFirewallOrderedInsertionExecutor> executor = CreateExecutor();
        FirewallOrderedInsertionService service = new(
            verifier.Object, nonceStore.Object, gate, guard.Object, executor.Object);
        InsertRuleRequest request = CreateRequest();

        IResponsePayload[] results = await Task.WhenAll(
            service.InsertAsync(request, TestContext.CancellationToken).AsTask(),
            service.InsertAsync(request, TestContext.CancellationToken).AsTask());

        Assert.AreEqual(1, results.Count(static result => result is RuleInsertionResponse));
        Assert.AreEqual(1, results.Count(static result => result is ConflictResponse));
        executor.Verify(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task InsertAsync_ReplayIsRejectedAfterNonceStoreRestartAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ufw-insert-nonce-tests", Guid.NewGuid().ToString("N"));
        string noncePath = Path.Combine(directory, "nonces");
        Directory.CreateDirectory(directory);
        try
        {
            TestTimeProvider clock = new(DateTimeOffset.FromUnixTimeSeconds(EXPIRES_AT_UNIX - 100));
            TestConfiguration configuration = new(TestAppSettingsFactory.Create(nonceStorePath: noncePath));
            using UfwExecutionGate gate = new();
            Mock<IIntentVerifier> verifier = CreateVerifier();
            Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
            Mock<IFirewallOrderedInsertionExecutor> executor = CreateExecutor();
            InsertRuleRequest request = CreateRequest();

            using (FileNonceStore firstStore = new(configuration, clock))
            {
                FirewallOrderedInsertionService first = new(
                    verifier.Object, firstStore, gate, guard.Object, executor.Object);
                Assert.IsInstanceOfType<RuleInsertionResponse>(
                    await first.InsertAsync(request, TestContext.CancellationToken));
            }

            using FileNonceStore restartedStore = new(configuration, clock);
            FirewallOrderedInsertionService restarted = new(
                verifier.Object, restartedStore, gate, guard.Object, executor.Object);
            Assert.IsInstanceOfType<ConflictResponse>(
                await restarted.InsertAsync(request, TestContext.CancellationToken));
            executor.Verify(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(0, RuleInsertionOutcome.Completed)]
    [DataRow(1, RuleInsertionOutcome.StaleBaseline)]
    [DataRow(2, RuleInsertionOutcome.PreconditionFailed)]
    [DataRow(3, RuleInsertionOutcome.StateUncertain)]
    public async Task InsertAsync_MapsTypedExecutionOutcomeAsync(
        int executionOutcomeValue,
        RuleInsertionOutcome responseOutcome)
    {
        RuleInsertionExecutionOutcome executionOutcome = (RuleInsertionExecutionOutcome)executionOutcomeValue;
        using UfwExecutionGate gate = new();
        Mock<IIntentVerifier> verifier = CreateVerifier();
        Mock<INonceStore> nonceStore = CreateNonceStore();
        Mock<IFirewallMutationSafetyGuard> guard = CreateSafetyGuard();
        Mock<IFirewallOrderedInsertionExecutor> executor = new(MockBehavior.Strict);
        RuleListResponse snapshot = new(Active: true, []);
        executor.Setup(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleInsertionExecutionResult(executionOutcome, snapshot, null, "diagnostic"));
        FirewallOrderedInsertionService service = new(
            verifier.Object, nonceStore.Object, gate, guard.Object, executor.Object);

        IResponsePayload result = await service.InsertAsync(CreateRequest(), TestContext.CancellationToken);

        RuleInsertionResponse response = Assert.IsInstanceOfType<RuleInsertionResponse>(result);
        Assert.AreEqual(responseOutcome, response.Outcome);
        Assert.AreSame(snapshot, response.FinalSnapshot);
        Assert.AreEqual("diagnostic", response.Diagnostic);
    }

    private static Mock<IIntentVerifier> CreateVerifier()
    {
        Mock<IIntentVerifier> verifier = new(MockBehavior.Strict);
        verifier.Setup(value => value.VerifyInsert(It.IsAny<InsertRuleRequest>()))
            .Returns(new IntentVerificationResult.AcceptedInsertion("key", NONCE, EXPIRES_AT_UNIX, Payload()));
        return verifier;
    }

    private static Mock<INonceStore> CreateNonceStore()
    {
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return nonceStore;
    }

    private static Mock<IFirewallMutationSafetyGuard> CreateSafetyGuard()
    {
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return guard;
    }

    private static Mock<IFirewallOrderedInsertionExecutor> CreateExecutor()
    {
        Mock<IFirewallOrderedInsertionExecutor> executor = new(MockBehavior.Strict);
        executor.Setup(value => value.ExecuteAsync(It.IsAny<InsertRulePayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompletedResult());
        return executor;
    }

    private static RuleInsertionExecutionResult CompletedResult() =>
        new(RuleInsertionExecutionOutcome.Completed, new RuleListResponse(Active: true, []), null, null);

    private static InsertRuleRequest CreateRequest() => new()
    {
        Version = IntentProtocol.VERSION,
        DeploymentId = "deployment",
        KeyId = "key",
        IssuedAtUnix = 1,
        Nonce = NONCE,
        Operation = IntentOperations.INSERT_RULE,
        Payload = JsonSerializer.SerializeToElement(Payload()),
        Signature = "signature",
    };

    private static InsertRulePayload Payload() => new()
    {
        BaselineFingerprint = FirewallRuleSnapshotFingerprint.Compute(active: true, []),
        AnchorOccurrenceId = 0,
        Placement = RuleInsertionPlacement.Before,
        Rule = new FirewallRuleSpecification
        {
            Action = FirewallAction.Allow,
            AddressFamily = FirewallAddressFamily.IPv4,
            Direction = FirewallDirection.In,
            Protocol = FirewallProtocol.Tcp,
        },
    };
}
