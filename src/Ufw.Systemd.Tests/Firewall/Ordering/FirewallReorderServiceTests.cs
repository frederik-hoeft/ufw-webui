using Moq;
using Ufw.Systemd.Firewall;
using Ufw.Systemd.Firewall.Ordering;

namespace Ufw.Systemd.Tests.Firewall.Ordering;

[TestClass]
public sealed class FirewallReorderServiceTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task ReorderAsync_HoldsSharedExecutionGateForEntireExecutorCallAsync()
    {
        using UfwExecutionGate gate = new();
        Mock<IFirewallMutationSafetyGuard> safetyGuard = new(MockBehavior.Strict);
        safetyGuard
            .Setup(candidate => candidate.EnsureSafeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IFirewallReorderExecutor> executor = new(MockBehavior.Strict);
        TaskCompletionSource executorEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseExecutor = new(TaskCreationOptions.RunContinuationsAsynchronously);
        executor
            .Setup(candidate => candidate.ExecuteAsync(It.IsAny<RuleReorderExecutionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                executorEntered.TrySetResult();
                await releaseExecutor.Task;
                return new RuleReorderExecutionResult(
                    RuleReorderExecutionOutcome.Completed,
                    null,
                    [],
                    [],
                    [],
                    null);
            });
        FirewallReorderService service = new(gate, safetyGuard.Object, executor.Object);
        RuleReorderExecutionRequest request = new("sha256:test", [0]);

        Task<RuleReorderExecutionResult> reorder = service.ReorderAsync(request, TestContext.CancellationToken);
        await executorEntered.Task.WaitAsync(TestContext.CancellationToken);
        bool secondEntered = false;
        Task<bool> competing = gate.RunAsync(_ =>
        {
            secondEntered = true;
            return Task.FromResult(true);
        }, TestContext.CancellationToken);

        await Task.Delay(20, TestContext.CancellationToken);
        Assert.IsFalse(secondEntered);
        releaseExecutor.TrySetResult();
        _ = await reorder;
        Assert.IsTrue(await competing);
        Assert.IsTrue(secondEntered);
    }
}
