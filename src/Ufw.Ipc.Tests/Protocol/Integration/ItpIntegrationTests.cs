using System.Buffers.Binary;
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
        await using IRequestMessage request = await context.MessageSerializer.SerializeRequestAsync(
            "/api/v1/raw-ok",
            RequestMethod.Get.ToString(),
            cancellationToken);
        byte[] frame = BuildFrame(ItpPacketType.ApplicationData, context.MessageSerializer.Encode(request));

        await using Stream stream = await context.ConnectRawAsync(cancellationToken);
        for (int i = 0; i < frame.Length; i++)
        {
            await stream.WriteAsync(frame.AsMemory(i, 1), cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        ItpFrame responseFrame = await new ItpConnection(stream).ReadAsync(cancellationToken);
        Assert.AreEqual(ItpPayloadFormat.IpcJson, responseFrame.PayloadFormat);
        await using IMessage decodedResponse = context.MessageSerializer.Decode(responseFrame.Payload);
        Assert.IsTrue(decodedResponse is IResponseMessage);
        IResponseMessage response = (IResponseMessage)decodedResponse;
        Assert.AreEqual(ApplicationMessageKind.Response, response.Kind);
        Assert.AreEqual(200, response.StatusCode);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task WrongItpVersionPreamble_DoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] preamble = [(byte)'I', (byte)'T', (byte)'P', 9];

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(preamble, cancellationToken);
        });
        Assert.AreEqual(ItpErrorCode.IncompleteFrame, exception.ErrorCode);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task UnsupportedPayloadFormat_DoesNotReachApplicationAndDoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] frame = BuildFrame(
            ItpPacketType.ApplicationData,
            "not-json-and-must-not-be-decoded"u8,
            payloadFormat: (ItpPayloadFormat)0x7F);

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.AreEqual(ItpErrorCode.UnsupportedPayloadFormat, exception.ErrorCode);
        Assert.IsTrue(exception.IsPeerReported);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task UnknownPacketType_DoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] frame = BuildFrame((ItpPacketType)0x3C, "???"u8.ToArray());
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.AreEqual(ItpErrorCode.UnsupportedPacketType, exception.ErrorCode);
        Assert.IsTrue(exception.IsPeerReported);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task PeerTransportError_DoesNotTriggerTransportErrorResponseLoop() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] peerErrorPayload =
        [
            0x00, (byte)ItpErrorCode.UnsupportedPacketType,
            0x00, 0x04,
            (byte)'n', (byte)'o', (byte)'p', (byte)'e',
        ];
        byte[] frame = BuildFrame(ItpPacketType.TransportError, peerErrorPayload);

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.AreEqual(ItpErrorCode.IncompleteFrame, exception.ErrorCode);
        Assert.IsFalse(exception.IsPeerReported);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task MalformedPeerTransportError_DoesNotTriggerTransportErrorResponseLoop() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] frame = BuildFrame(ItpPacketType.TransportError, [0x00, 0x03]);

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
        {
            await using IMessage _ = await context.ExchangeBytesAsync(frame, cancellationToken);
        });
        Assert.AreEqual(ItpErrorCode.IncompleteFrame, exception.ErrorCode);
        Assert.IsFalse(exception.IsPeerReported);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task InvalidApplicationJson_ReturnsBadRequestAndKeepsWorker() => RunAsync(async (context, cancellationToken) =>
    {
        byte[] frame = BuildFrame(ItpPacketType.ApplicationData, "{}"u8.ToArray());
        await using IResponseMessage response = await context.ExchangeBytesAsync(frame, cancellationToken);
        Assert.AreEqual(400, response.StatusCode);
        Assert.AreEqual(ApplicationPayloadTypes.ERROR, response.PayloadType);

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task ResponseDocumentSentAsRequest_ReturnsBadRequest() => RunAsync(async (context, cancellationToken) =>
    {
        await using IResponseMessage responseDocument = await context.MessageSerializer.SerializeResponseAsync(
            new OkResponse(),
            cancellationToken);
        await using IResponseMessage response = await context.ExchangeApplicationBytesAsync(
            context.MessageSerializer.Encode(responseDocument),
            cancellationToken);
        Assert.AreEqual(400, response.StatusCode);
        Assert.AreEqual(ApplicationPayloadTypes.ERROR, response.PayloadType);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    [TestMethod]
    public Task PeerCloseWithoutFrame_DoesNotKillWorker() => RunAsync(async (context, cancellationToken) =>
    {
        await using Stream stream = await context.ConnectRawAsync(cancellationToken);
        await stream.DisposeAsync();

        OkResponse ok = await context.SendAsync<OkResponse>(RequestMethod.Get, "/api/v1/raw-ok", cancellationToken);
        Assert.IsNotNull(ok);
    }, cancellationToken: TestContext.CancellationToken).AsTask();

    private static byte[] BuildFrame(
        ItpPacketType packetType,
        ReadOnlySpan<byte> payload,
        byte version = ItpConstants.VERSION,
        ItpPayloadFormat? payloadFormat = null)
    {
        ItpPayloadFormat effectivePayloadFormat = payloadFormat ?? (packetType == ItpPacketType.ApplicationData
            ? ItpPayloadFormat.IpcJson
            : ItpPayloadFormat.None);
        byte[] frame = new byte[ItpConstants.VERSION_1_HEADER_SIZE + payload.Length];
        "ITP"u8.CopyTo(frame);
        frame[3] = version;
        frame[4] = (byte)packetType;
        frame[5] = (byte)effectivePayloadFormat;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(6, 4), (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(ItpConstants.VERSION_1_HEADER_SIZE));
        return frame;
    }
}
