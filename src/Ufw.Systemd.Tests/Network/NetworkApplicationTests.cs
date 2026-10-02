using Ufw.Systemd.Network;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Tests.TestSupport;

namespace Ufw.Systemd.Tests.Network;

[TestClass]
public sealed class NetworkApplicationTests
{
    [TestMethod]
    public async Task RunAsync_UnexpectedWorkerFailureCancelsSiblingsAndPropagatesFailureAsync()
    {
        InvalidOperationException expected = new("worker invariant failed");
        ControlledWorker worker = new(workerCount: 3, expected);
        NetworkApplication application = new(new TestConfiguration(TestAppSettingsFactory.Create(maxConnections: 3)), worker, new ConsoleLogger());

        Task runTask = application.RunAsync(CancellationToken.None);
        await worker.AllWorkersStarted;
        worker.FailFirstWorker();

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await runTask);
        Assert.AreSame(expected, actual);
        await worker.SiblingWorkersStopped;
        Assert.AreEqual(2, worker.CancelledSiblingCount);
    }

    [TestMethod]
    public async Task RunAsync_WorkerReturningWithoutApplicationCancellationFailsPoolAsync()
    {
        ControlledWorker worker = new(workerCount: 2, failure: null);
        NetworkApplication application = new(new TestConfiguration(TestAppSettingsFactory.Create(maxConnections: 2)), worker, new ConsoleLogger());

        Task runTask = application.RunAsync(CancellationToken.None);
        await worker.AllWorkersStarted;
        worker.CompleteFirstWorker();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await runTask);
        StringAssert.Contains(exception.Message, "network worker stopped", StringComparison.OrdinalIgnoreCase);
        await worker.SiblingWorkersStopped;
        Assert.AreEqual(1, worker.CancelledSiblingCount);
    }

    [TestMethod]
    public async Task RunAsync_ApplicationCancellationStopsWholePoolCleanlyAsync()
    {
        using CancellationTokenSource cancellation = new();
        ControlledWorker worker = new(workerCount: 3, failure: null);
        NetworkApplication application = new(new TestConfiguration(TestAppSettingsFactory.Create(maxConnections: 3)), worker, new ConsoleLogger());

        Task runTask = application.RunAsync(cancellation.Token);
        await worker.AllWorkersStarted;
        await cancellation.CancelAsync();

        await runTask;
        await worker.AllWorkersStopped;
        Assert.AreEqual(3, worker.CancelledWorkerCount);
    }

    private sealed class ControlledWorker : INetworkApplicationWorker
    {
        private readonly int _workerCount;
        private readonly Exception? _failure;
        private readonly TaskCompletionSource _allWorkersStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _firstWorkerRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allWorkersStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _siblingWorkersStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _startedWorkers;
        private int _stoppedWorkers;
        private int _cancelledWorkers;
        private int _cancelledSiblings;

        public ControlledWorker(int workerCount, Exception? failure)
        {
            _workerCount = workerCount;
            _failure = failure;
            if (workerCount == 1)
            {
                _siblingWorkersStopped.TrySetResult();
            }
        }

        public Task AllWorkersStarted => _allWorkersStarted.Task;

        public Task AllWorkersStopped => _allWorkersStopped.Task;

        public Task SiblingWorkersStopped => _siblingWorkersStopped.Task;

        public int CancelledWorkerCount => Volatile.Read(ref _cancelledWorkers);

        public int CancelledSiblingCount => Volatile.Read(ref _cancelledSiblings);

        public async Task ServeAsync(CancellationToken cancellationToken)
        {
            int workerNumber = Interlocked.Increment(ref _startedWorkers);
            if (workerNumber == _workerCount)
            {
                _allWorkersStarted.TrySetResult();
            }

            try
            {
                if (workerNumber == 1)
                {
                    await _firstWorkerRelease.Task.WaitAsync(cancellationToken);
                    if (_failure is not null)
                    {
                        throw _failure;
                    }
                    return;
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref _cancelledWorkers);
                if (workerNumber != 1 && Interlocked.Increment(ref _cancelledSiblings) == _workerCount - 1)
                {
                    _siblingWorkersStopped.TrySetResult();
                }
            }
            finally
            {
                if (Interlocked.Increment(ref _stoppedWorkers) == _workerCount)
                {
                    _allWorkersStopped.TrySetResult();
                }
            }
        }

        public void FailFirstWorker() => _firstWorkerRelease.TrySetResult();

        public void CompleteFirstWorker() => _firstWorkerRelease.TrySetResult();
    }
}
