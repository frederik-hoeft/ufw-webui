using Moq;
using Ufw.Shared.Firewall;
using Ufw.Shared.Ipc.Model;
using Ufw.Shared.Ipc.Model.Responses;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Security.Intent;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class SignedMutationOrchestratorTests
{
    private const string NONCE = "test-nonce";
    private const long EXPIRES_AT_UNIX = 2_000_000_000;

    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ExecuteAsync_RunsGateSafetyNonceAndOperationInOrderAsync()
    {
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IUfwExecutionGate> gate = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        List<string> calls = [];
        gate.Setup(value => value.RunAsync(It.IsAny<Func<CancellationToken, Task<IResponsePayload>>>(), TestContext.CancellationToken))
            .Returns<Func<CancellationToken, Task<IResponsePayload>>, CancellationToken>(async (action, cancellationToken) =>
            {
                calls.Add("gate");
                return await action(cancellationToken);
            });
        guard.Setup(value => value.EnsureSafeAsync(TestContext.CancellationToken)).Returns(() =>
        {
            calls.Add("guard");
            return Task.CompletedTask;
        });
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, TestContext.CancellationToken)).Returns(() =>
        {
            calls.Add("nonce");
            return ValueTask.FromResult(true);
        });
        SignedMutationOrchestrator orchestrator = new(nonceStore.Object, gate.Object, guard.Object, CreateCleanSnapshotReader());
        OkResponse expected = new();

        IResponsePayload response = await orchestrator.ExecuteAsync(Accepted(), cancellationToken =>
        {
            Assert.AreEqual(TestContext.CancellationToken, cancellationToken);
            calls.Add("operation");
            return Task.FromResult<IResponsePayload>(expected);
        }, TestContext.CancellationToken);

        Assert.AreSame(expected, response);
        CollectionAssert.AreEqual(new List<string> { "gate", "guard", "nonce", "operation" }, calls);
    }

    [TestMethod]
    public async Task ExecuteAsync_DuplicateBaselineRejectsBeforeNonceAndOperationAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Mock<IFirewallRuleSnapshotReader> reader = new(MockBehavior.Strict);
        reader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirewallRuleSnapshotReadResult.Success(new Ufw.Shared.Ipc.Model.Responses.Domain.RuleListResponse(true,
            [
                new Ufw.Shared.Firewall.ListedFirewallRule { RuleId = "sha256:same" },
                new Ufw.Shared.Firewall.ListedFirewallRule { RuleId = "sha256:same" },
            ], TestFirewallConfiguration.Enabled)));
        SignedMutationOrchestrator orchestrator = new(nonceStore.Object, gate, guard.Object, reader.Object);
        bool operationRan = false;

        IResponsePayload response = await orchestrator.ExecuteAsync(Accepted(), _ =>
        {
            operationRan = true;
            return Task.FromResult<IResponsePayload>(new OkResponse());
        }, TestContext.CancellationToken);

        UnprocessableContentResponse error = Assert.IsInstanceOfType<UnprocessableContentResponse>(response);
        StringAssert.Contains(error.Message, "duplicate semantic rule identities");
        Assert.AreEqual(FirewallStateErrorCodes.AMBIGUOUS_STATE, error.Code);
        Assert.IsFalse(operationRan);
        nonceStore.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ExecuteAsync_SafetyFailureDoesNotConsumeNonceOrRunOperationAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("recovery blocked"));
        SignedMutationOrchestrator orchestrator = new(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader());
        bool operationRan = false;

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            _ = await orchestrator.ExecuteAsync(Accepted(), _ =>
            {
                operationRan = true;
                return Task.FromResult<IResponsePayload>(new OkResponse());
            }, TestContext.CancellationToken));

        StringAssert.Contains(exception.Message, "recovery");
        Assert.IsFalse(operationRan);
        nonceStore.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ExecuteAsync_DuplicateNonceReturnsConflictWithoutRunningOperationAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        SignedMutationOrchestrator orchestrator = new(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader());
        bool operationRan = false;

        IResponsePayload response = await orchestrator.ExecuteAsync(Accepted(), _ =>
        {
            operationRan = true;
            return Task.FromResult<IResponsePayload>(new OkResponse());
        }, TestContext.CancellationToken);

        ConflictResponse conflict = Assert.IsInstanceOfType<ConflictResponse>(response);
        Assert.AreEqual("Intent nonce has already been used.", conflict.Message);
        Assert.IsFalse(operationRan);
    }

    [TestMethod]
    public async Task ExecuteAsync_HoldsExecutionGateUntilOperationCompletesAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        nonceStore.Setup(value => value.TryConsumeAsync(NONCE, EXPIRES_AT_UNIX, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        guard.Setup(value => value.EnsureSafeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        SignedMutationOrchestrator orchestrator = new(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader());
        TaskCompletionSource operationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<IResponsePayload> mutation = orchestrator.ExecuteAsync(Accepted(), async _ =>
        {
            operationEntered.TrySetResult();
            await releaseOperation.Task;
            return new OkResponse();
        }, TestContext.CancellationToken);
        await operationEntered.Task.WaitAsync(TestContext.CancellationToken);

        bool competingEntered = false;
        Task competing = gate.RunAsync(_ =>
        {
            competingEntered = true;
            return Task.CompletedTask;
        }, TestContext.CancellationToken);

        await Task.Delay(20, TestContext.CancellationToken);
        Assert.IsFalse(competingEntered);
        releaseOperation.TrySetResult();
        await mutation;
        await competing;
        Assert.IsTrue(competingEntered);
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationWhileQueuedDoesNotEnterMutationBoundaryAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<INonceStore> nonceStore = new(MockBehavior.Strict);
        Mock<IFirewallMutationSafetyGuard> guard = new(MockBehavior.Strict);
        SignedMutationOrchestrator orchestrator = new(nonceStore.Object, gate, guard.Object, CreateCleanSnapshotReader());
        TaskCompletionSource blockerEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseBlocker = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task blocker = gate.RunAsync(async _ =>
        {
            blockerEntered.TrySetResult();
            await releaseBlocker.Task;
        }, TestContext.CancellationToken);
        await blockerEntered.Task.WaitAsync(TestContext.CancellationToken);
        using CancellationTokenSource cancellation = new();
        bool operationRan = false;

        Task<IResponsePayload> mutation = orchestrator.ExecuteAsync(Accepted(), _ =>
        {
            operationRan = true;
            return Task.FromResult<IResponsePayload>(new OkResponse());
        }, cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => _ = await mutation);
        Assert.IsFalse(operationRan);
        guard.VerifyNoOtherCalls();
        nonceStore.VerifyNoOtherCalls();
        releaseBlocker.TrySetResult();
        await blocker;
    }

    private static IntentVerificationResult.AcceptedRuleMutation Accepted() =>
        new("key", NONCE, EXPIRES_AT_UNIX, new FirewallRuleSpecification(), null);

    private static IFirewallRuleSnapshotReader CreateCleanSnapshotReader()
    {
        Mock<IFirewallRuleSnapshotReader> reader = new();
        reader.Setup(value => value.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FirewallRuleSnapshotReadResult.Success(new Ufw.Shared.Ipc.Model.Responses.Domain.RuleListResponse(
                true, [], Ufw.Systemd.Tests.TestFirewallConfiguration.Enabled)));
        return reader.Object;
    }
}
