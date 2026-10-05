using Moq;
using System.Net.Sockets;
using System.Security.Authentication;
using Ufw.Shared.Ipc.Transport;
using Ufw.Systemd.Network;
using Ufw.Systemd.Services.Logging;
using Ufw.Systemd.Transport;

namespace Ufw.Systemd.Tests.Network;

[TestClass]
public sealed class NetworkApplicationWorkerTests
{
    [TestMethod]
    public async Task ServeAsync_ExpectedConnectionFailuresDoNotConsumeWorkerAsync()
    {
        Exception[] expectedFailures =
        [
            new OperationCanceledException("connection cancelled"),
            new SocketException((int)SocketError.ConnectionReset),
            new InvalidDataException("invalid frame"),
            new AuthenticationException("TLS rejected"),
            new TimeoutException("request timed out"),
            new IOException("peer disconnected"),
        ];

        foreach (Exception expectedFailure in expectedFailures)
        {
            using CancellationTokenSource cancellation = new();
            Mock<ITransportLayerConnection> connection = new();
            Mock<ITransportLayerService> transport = new();
            Mock<INetworkConnectionProcessor> processor = new();
            int requests = 0;
            transport.Setup(service => service.ServeAsync(cancellation.Token)).ReturnsAsync(connection.Object);
            processor
                .Setup(service => service.ProcessAsync(connection.Object, It.IsAny<Guid>(), cancellation.Token))
                .Returns(async () =>
                {
                    int request = Interlocked.Increment(ref requests);
                    if (request == 1)
                    {
                        throw expectedFailure;
                    }

                    await cancellation.CancelAsync();
                });

            NetworkApplicationWorker worker = new(transport.Object, processor.Object, new ConsoleLogger());
            await worker.ServeAsync(cancellation.Token);

            Assert.AreEqual(2, requests, $"Worker stopped after {expectedFailure.GetType().Name}.");
        }
    }

    [TestMethod]
    public async Task ServeAsync_UnexpectedConnectionFailureFaultsWorkerAsync()
    {
        InvalidOperationException expected = new("processor invariant failed");
        Mock<ITransportLayerConnection> connection = new();
        Mock<ITransportLayerService> transport = new();
        Mock<INetworkConnectionProcessor> processor = new();
        transport.Setup(service => service.ServeAsync(CancellationToken.None)).ReturnsAsync(connection.Object);
        processor.Setup(service => service.ProcessAsync(connection.Object, It.IsAny<Guid>(), CancellationToken.None)).ThrowsAsync(expected);
        NetworkApplicationWorker worker = new(transport.Object, processor.Object, new ConsoleLogger());

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await worker.ServeAsync(CancellationToken.None));

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task ServeAsync_ApplicationCancellationStopsWorkerCleanlyAsync()
    {
        using CancellationTokenSource cancellation = new();
        Mock<ITransportLayerService> transport = new();
        Mock<INetworkConnectionProcessor> processor = new(MockBehavior.Strict);
        transport
            .Setup(service => service.ServeAsync(cancellation.Token))
            .Returns<CancellationToken>(async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new AssertFailedException("Cancellation should interrupt the pending accept.");
            });
        NetworkApplicationWorker worker = new(transport.Object, processor.Object, new ConsoleLogger());

        Task workerTask = worker.ServeAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await workerTask;
        processor.VerifyNoOtherCalls();
    }
}
