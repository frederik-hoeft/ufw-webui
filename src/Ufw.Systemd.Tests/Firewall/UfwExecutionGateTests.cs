using Ufw.Systemd.Firewall;

namespace Ufw.Systemd.Tests.Firewall;

[TestClass]
public sealed class UfwExecutionGateTests
{
    public required TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RunAsync_NonGeneric_SerializesActionsAsync()
    {
        using UfwExecutionGate gate = new();
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task first = gate.RunAsync(async _ =>
        {
            firstEntered.TrySetResult();
            await releaseFirst.Task;
        }, TestContext.CancellationToken);
        await firstEntered.Task.WaitAsync(TestContext.CancellationToken);

        Task second = gate.RunAsync(_ =>
        {
            secondEntered.TrySetResult();
            return Task.CompletedTask;
        }, TestContext.CancellationToken);

        await Task.Delay(20, TestContext.CancellationToken);
        Assert.IsFalse(secondEntered.Task.IsCompleted);

        releaseFirst.TrySetResult();
        await Task.WhenAll(first, second);
        Assert.IsTrue(secondEntered.Task.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task RunAsync_NonGeneric_CancellationWhileWaiting_DoesNotEnterActionAsync()
    {
        using UfwExecutionGate gate = new();
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource waitingCancellation = new();
        bool secondEntered = false;

        Task first = gate.RunAsync(async _ =>
        {
            firstEntered.TrySetResult();
            await releaseFirst.Task;
        }, TestContext.CancellationToken);
        await firstEntered.Task.WaitAsync(TestContext.CancellationToken);

        Task second = gate.RunAsync(_ =>
        {
            secondEntered = true;
            return Task.CompletedTask;
        }, waitingCancellation.Token);
        await waitingCancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await second);
        Assert.IsFalse(secondEntered);

        releaseFirst.TrySetResult();
        await first;
    }

    [TestMethod]
    public void RunAsync_NonGeneric_AfterDispose_ThrowsObjectDisposedException()
    {
        UfwExecutionGate gate = new();
        gate.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => gate.RunAsync(static _ => Task.CompletedTask, CancellationToken.None));
    }
}
