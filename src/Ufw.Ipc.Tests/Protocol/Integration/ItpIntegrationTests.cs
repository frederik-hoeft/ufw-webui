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
        Assert.AreEqual(ItpPayloadFormat.IpcJson, responseFrame.PayloadFormat);
        await using IMessage response = context.MessageSerializer.Decode(responseFrame.Payload);
        Assert.AreEqual(ApplicationMessageKind.Response, response.Kind);
        Assert.AreEqual(200, response.StatusCode);
    }).AsTask();

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
    }).AsTask();

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
    }).AsTask();

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

    private static byte[] BuildFrame(
        ItpPacketType packetType,
        ReadOnlySpan<byte> payload,
        byte version = ItpConstants.Version,
        ItpPayloadFormat? payloadFormat = null)
    {
        ItpPayloadFormat effectivePayloadFormat = payloadFormat ?? (packetType == ItpPacketType.ApplicationData
            ? ItpPayloadFormat.IpcJson
            : ItpPayloadFormat.None);
        byte[] frame = new byte[ItpConstants.Version1HeaderSize + payload.Length];
        "ITP"u8.CopyTo(frame);
        frame[3] = version;
        frame[4] = (byte)packetType;
        frame[5] = (byte)effectivePayloadFormat;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(6, 4), (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(ItpConstants.Version1HeaderSize));
        return frame;
    }
}
