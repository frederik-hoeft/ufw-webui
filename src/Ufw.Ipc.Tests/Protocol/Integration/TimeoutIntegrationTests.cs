using System.Buffers.Binary;
using System.Diagnostics;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
public sealed class TimeoutIntegrationTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureOptionsAsync(IpcTestOptions options, CancellationToken cancellationToken)
    {
        options.WorkerCount = 1;
        options.TestTimeout = TimeSpan.FromSeconds(10);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask ConfigureEndpointsAsync(ITestEndpointMapBuilder endpoints, CancellationToken cancellationToken)
    {
        endpoints.MapGet("/api/v1/timeout-ok", static _ => ValueTask.FromResult(new OkResponse()));
        endpoints.MapGet(
            "/api/v1/block",
            static async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new OkResponse();
            });
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public Task TestSilentPeer_IsClosedByIoTimeout_AndWorkerRecovers() => RunAsync(
        static async (context, cancellationToken) =>
        {
            await using Stream stream = await context.ConnectRawAsync(cancellationToken);
            await AssertRemoteClosedAsync(stream, cancellationToken);

            OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/timeout-ok", cancellationToken);
            Assert.IsNotNull(response);
        },
        TimeoutConfiguration(ioTimeout: TimeSpan.FromMilliseconds(100), requestTimeout: TimeSpan.FromSeconds(2)), TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestPartialPreamble_IsClosedByIoTimeout_AndWorkerRecovers() => RunAsync(
        static async (context, cancellationToken) =>
        {
            await using Stream stream = await context.ConnectRawAsync(cancellationToken);
            await stream.WriteAsync("IT"u8.ToArray(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            await AssertRemoteClosedAsync(stream, cancellationToken);

            OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/timeout-ok", cancellationToken);
            Assert.IsNotNull(response);
        },
        TimeoutConfiguration(ioTimeout: TimeSpan.FromMilliseconds(100), requestTimeout: TimeSpan.FromSeconds(2)), TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestPartialPayload_IsClosedByIoTimeout_AndWorkerRecovers() => RunAsync(
        static async (context, cancellationToken) =>
        {
            await using Stream stream = await context.ConnectRawAsync(cancellationToken);
            byte[] partialFrame = BuildPartialApplicationFrame(declaredPayloadLength: 32, "{}"u8);
            await stream.WriteAsync(partialFrame, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            await AssertRemoteClosedAsync(stream, cancellationToken);

            OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/timeout-ok", cancellationToken);
            Assert.IsNotNull(response);
        },
        TimeoutConfiguration(ioTimeout: TimeSpan.FromMilliseconds(100), requestTimeout: TimeSpan.FromSeconds(2)), TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestSlowTrickle_ExceedsOverallRequestDeadline() => RunAsync(
        static async (context, cancellationToken) =>
        {
            await using Stream stream = await context.ConnectRawAsync(cancellationToken);
            byte[] frame = BuildPartialApplicationFrame(declaredPayloadLength: 256, new byte[256]);
            byte[] responseBuffer = new byte[1];
            Task<int> remoteRead = stream.ReadAsync(responseBuffer, CancellationToken.None).AsTask();
            Stopwatch stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < frame.Length && !remoteRead.IsCompleted; i++)
            {
                try
                {
                    await stream.WriteAsync(frame.AsMemory(i, 1), cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }
                catch (IOException)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }

            int bytesRead = await remoteRead.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            Assert.AreEqual(0, bytesRead);
            Assert.IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450), stopwatch.Elapsed);
            Assert.IsLessThan(TimeSpan.FromSeconds(2), stopwatch.Elapsed);

            OkResponse response = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/timeout-ok", cancellationToken);
            Assert.IsNotNull(response);
        },
        TimeoutConfiguration(ioTimeout: TimeSpan.FromMilliseconds(400), requestTimeout: TimeSpan.FromMilliseconds(700)), TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestCallerCancellation_RemainsOperationCanceledException() => RunAsync(
        static async (context, cancellationToken) =>
        {
            using CancellationTokenSource callerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            callerCts.CancelAfter(TimeSpan.FromMilliseconds(150));

            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/block", callerCts.Token));
        },
        TimeoutConfiguration(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan), TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task TestClientRequestDeadline_SurfacesTimeoutException() => RunAsync(
        static async (context, cancellationToken) =>
        {
            await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
                _ = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/block", cancellationToken));
        },
        new IpcTestRunConfiguration
        {
            ConfigureOptions = static options =>
            {
                options.IoTimeout = Timeout.InfiniteTimeSpan;
                options.RequestTimeout = TimeSpan.FromSeconds(2);
                options.ClientIoTimeout = Timeout.InfiniteTimeSpan;
                options.ClientRequestTimeout = TimeSpan.FromMilliseconds(150);
            },
        }, TestContext.CancellationToken).AsTask();

    [TestMethod]
    public async Task DaemonShutdownCancellation_InterruptsBlockedReadAsync()
    {
        using CancellationTokenSource shutdownCts = new();
        await RunAsync(
            async (context, _) =>
            {
                await using Stream stream = await context.ConnectRawAsync(CancellationToken.None);
                byte[] buffer = new byte[1];
                Task<int> read = stream.ReadAsync(buffer, CancellationToken.None).AsTask();

                await shutdownCts.CancelAsync();

                int bytesRead = await read.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
                Assert.AreEqual(0, bytesRead);
            },
            TimeoutConfiguration(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan),
            shutdownCts.Token);
    }

    [TestMethod]
    public Task TestInfiniteTimeouts_DoNotCreateInternalTimeout() => RunAsync(
        static async (context, cancellationToken) =>
        {
            await using Stream stream = await context.ConnectRawAsync(cancellationToken);
            using CancellationTokenSource readCts = new();
            byte[] buffer = new byte[1];
            Task<int> read = stream.ReadAsync(buffer, readCts.Token).AsTask();

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            Assert.IsFalse(read.IsCompleted);

            await readCts.CancelAsync();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await read);
        },
        TimeoutConfiguration(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan), TestContext.CancellationToken).AsTask();

    private static IpcTestRunConfiguration TimeoutConfiguration(TimeSpan ioTimeout, TimeSpan requestTimeout) =>
        new()
        {
            ConfigureOptions = options =>
            {
                options.IoTimeout = ioTimeout;
                options.RequestTimeout = requestTimeout;
            },
        };

    private static async Task AssertRemoteClosedAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1];
        int bytesRead = await stream.ReadAsync(buffer, CancellationToken.None).AsTask().WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
        Assert.AreEqual(0, bytesRead);
    }

    private static byte[] BuildPartialApplicationFrame(int declaredPayloadLength, ReadOnlySpan<byte> payload)
    {
        byte[] frame = new byte[ItpConstants.VERSION_1_HEADER_SIZE + payload.Length];
        "ITP"u8.CopyTo(frame);
        frame[3] = ItpConstants.VERSION;
        frame[4] = (byte)ItpPacketType.ApplicationData;
        frame[5] = (byte)ItpPayloadFormat.IpcJson;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(6, sizeof(uint)), (uint)declaredPayloadLength);
        payload.CopyTo(frame.AsSpan(ItpConstants.VERSION_1_HEADER_SIZE));
        return frame;
    }
}
