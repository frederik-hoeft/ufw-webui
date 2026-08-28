using System.Buffers.Binary;
using System.Text;
using Ufw.Ipc.Shared.Model;
using Ufw.Ipc.Shared.Model.Responses;
using Ufw.Ipc.Shared.Protocol;
using Ufw.Ipc.Shared.Serialization;
using Ufw.Ipc.Shared.Transport.Itp;
using Ufw.Ipc.Tests.Adapter;
using Ufw.Ipc.Tests.Adapter.Endpoints;

namespace Ufw.Ipc.Tests.Protocol.Integration;

[TestClass]
public sealed class ItpIntegrationTests : IpcProtocolTestBase
{
    protected override ValueTask ConfigureOptionsAsync(IpcTestOptions options, CancellationToken cancellationToken)
    {
        options.WorkerCount = 1;
        options.RequestTimeout = TimeSpan.FromSeconds(5);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask ConfigureEndpointsAsync(ITestEndpointMapBuilder endpoints, CancellationToken cancellationToken)
    {
        endpoints.MapGet("/api/v1/raw-ok", static _ => ValueTask.FromResult(new OkResponse()));
        return ValueTask.CompletedTask;
    }

    [TestMethod]
    public Task FragmentedApplicationFrame_IsAccepted() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer.SerializeAsync(
            "/api/v1/raw-ok",
            RequestMethod.Get.ToString(),
            payload: (object?)null,
            typeof(object),
            cancellationToken);
        byte[] frame = BuildFrame(ItpPacketType.ApplicationData, context.MessageSerializer.Encode(request));

        await using Stream stream = await context.ConnectRawAsync(cancellationToken);
        for (int i = 0; i < frame.Length; i++)
        {
            await stream.WriteAsync(frame.AsMemory(i, 1), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        ItpFrame responseFrame = await new ItpConnection(stream).ReadAsync(cancellationToken);
        await using IMessage response = context.MessageSerializer.Decode(responseFrame.Payload);
        Assert.AreEqual(ApplicationMessageKind.Response, response.Kind);
        Assert.AreEqual(200, response.StatusCode);
    }).AsTask();

    [TestMethod]
    public Task WrongItpVersion_DoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer.SerializeAsync(
            "/api/v1/raw-ok",
            RequestMethod.Get.ToString(),
            payload: (object?)null,
            typeof(object),
            cancellationToken);
        byte[] frame = BuildFrame(ItpPacketType.ApplicationData, context.MessageSerializer.Encode(request), version: 9);

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.IsTrue(
            exception.ErrorCode is ItpErrorCode.VersionMismatch or ItpErrorCode.IncompleteFrame,
            exception.ToString());

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }).AsTask();

    [TestMethod]
    public Task GarbledCrc_DoesNotReachApplicationAndDoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage request = await context.MessageSerializer.SerializeAsync(
            "/api/v1/raw-ok",
            RequestMethod.Get.ToString(),
            payload: (object?)null,
            typeof(object),
            cancellationToken);
        byte[] frame = BuildFrame(ItpPacketType.ApplicationData, context.MessageSerializer.Encode(request));
        frame[^1] ^= 0xFF;

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.IsTrue(
            exception.ErrorCode is ItpErrorCode.InvalidChecksum or ItpErrorCode.IncompleteFrame,
            exception.ToString());

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }).AsTask();

    [TestMethod]
    public Task UnknownPacketType_DoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] frame = BuildFrame((ItpPacketType)0x3C, "???"u8.ToArray());
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.IsTrue(
            exception.ErrorCode is ItpErrorCode.UnsupportedPacketType or ItpErrorCode.IncompleteFrame,
            exception.ToString());

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }).AsTask();

    [TestMethod]
    public Task InvalidApplicationJson_ReturnsBadRequestAndKeepsWorker() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] frame = BuildFrame(ItpPacketType.ApplicationData, "{}"u8.ToArray());
        await using IMessage response = await context.ExchangeBytesAsync(frame, cancellationToken);
        Assert.AreEqual("400", response.Id);
        Assert.AreEqual(ApplicationPayloadTypes.Error, response.PayloadType);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }).AsTask();

    [TestMethod]
    public Task ResponseDocumentSentAsRequest_ReturnsBadRequest() => RunAsync(async (context, cancellationToken) =>
    {
        await using IMessage responseDocument = await context.MessageSerializer.SerializeAsync(
            new OkResponse(),
            cancellationToken);
        await using IMessage response = await context.ExchangeRawAsync(responseDocument, cancellationToken);
        Assert.AreEqual("400", response.Id);
        Assert.AreEqual(ApplicationPayloadTypes.Error, response.PayloadType);
    }).AsTask();

    [TestMethod]
    public Task PeerCloseWithoutFrame_DoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        await using Stream stream = await context.ConnectRawAsync(cancellationToken);
        await stream.DisposeAsync();

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }).AsTask();

    private static byte[] BuildFrame(ItpPacketType packetType, ReadOnlySpan<byte> payload, byte version = ItpConstants.Version)
    {
        byte[] frame = new byte[ItpConstants.HeaderSize + payload.Length + ItpConstants.TrailerSize];
        "ITP"u8.CopyTo(frame);
        frame[3] = version;
        frame[4] = (byte)packetType;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(6, 4), (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(ItpConstants.HeaderSize));
        uint crc = ItpCrc32.Compute(frame.AsSpan(0, ItpConstants.HeaderSize + payload.Length));
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(ItpConstants.HeaderSize + payload.Length, 4), crc);
        return frame;
    }
}
