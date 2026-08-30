using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Ufw.Ipc.Shared.Transport;
using Ufw.Ipc.Shared.Transport.Itp;

namespace Ufw.Ipc.Tests.Protocol.Itp;

[TestClass]
public sealed class ItpFrameCodecTests
{
    [TestMethod]
    public async Task RoundTrip_ApplicationData_PreservesPayloadAndFormat()
    {
        byte[] payload = "application-bytes"u8.ToArray();
        using MemoryStream stream = new();
        ItpConnection writer = new(stream);

        await writer.WriteApplicationDataAsync(payload);

        stream.Position = 0;
        ItpConnection reader = new(stream);
        ItpFrame frame = await reader.ReadAsync();

        Assert.AreEqual(ItpPacketType.ApplicationData, frame.PacketType);
        Assert.AreEqual(ItpPayloadFormat.IpcJson, frame.PayloadFormat);
        CollectionAssert.AreEqual(payload, frame.Payload.ToArray());
    }

    [TestMethod]
    public async Task RoundTrip_TransportError_SurfacesPeerError()
    {
        using MemoryStream stream = new();
        ItpConnection writer = new(stream);
        await writer.WriteTransportErrorAsync(ItpErrorCode.VersionMismatch, "peer is v2");

        stream.Position = 0;
        ItpConnection reader = new(stream);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () => await reader.ReadAsync());

        Assert.AreEqual(ItpErrorCode.VersionMismatch, exception.ErrorCode);
        Assert.IsTrue(exception.IsPeerReported);
        Assert.IsFalse(exception.CanReplyWithTransportError);
        StringAssert.Contains(exception.Message, "peer is v2");
    }

    [TestMethod]
    public async Task WriteTransportError_LongDiagnostic_IsBoundedAndValidUtf8()
    {
        using MemoryStream stream = new();
        await new ItpConnection(stream).WriteTransportErrorAsync(
            ItpErrorCode.InvalidFrame,
            new string('\u20AC', ItpConstants.MaxTransportErrorMessageUtf8Length));

        byte[] frame = stream.ToArray();
        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(6, 4));
        ushort messageLength = BinaryPrimitives.ReadUInt16BigEndian(
            frame.AsSpan(ItpConstants.Version1HeaderSize + 2, 2));

        Assert.AreEqual((uint)(4 + messageLength), payloadLength);
        Assert.IsTrue(messageLength <= ItpConstants.MaxTransportErrorMessageUtf8Length);

        stream.Position = 0;
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.IsTrue(exception.IsPeerReported);
        Assert.IsFalse(exception.Message.Contains('\uFFFD'));
    }

    [TestMethod]
    public async Task WriteApplicationData_EmptyPayload_IsRejected()
    {
        using MemoryStream stream = new();
        ItpConnection writer = new(stream);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await writer.WriteApplicationDataAsync(ReadOnlyMemory<byte>.Empty));
        Assert.AreEqual(ItpErrorCode.EmptyApplicationPayload, exception.ErrorCode);
        Assert.AreEqual(0, stream.Length);
    }

    [TestMethod]
    public async Task Read_FragmentedBytes_ReassemblesFrame()
    {
        byte[] payload = Encoding.UTF8.GetBytes(new string('x', 64));
        using MemoryStream raw = new();
        await new ItpConnection(raw).WriteApplicationDataAsync(payload);
        byte[] frameBytes = raw.ToArray();

        await using OneByteReadStream fragmented = new(frameBytes);
        ItpFrame frame = await new ItpConnection(fragmented).ReadAsync();
        CollectionAssert.AreEqual(payload, frame.Payload.ToArray());
    }

    [TestMethod]
    public async Task Read_TruncatedPreamble_IsIncompleteFrame()
    {
        using MemoryStream stream = new("ITP"u8.ToArray());
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.IncompleteFrame, exception.ErrorCode);
        Assert.IsFalse(exception.IsPeerReported);
        Assert.IsFalse(exception.CanReplyWithTransportError);
    }

    [TestMethod]
    public async Task Read_TruncatedVersion1Header_IsIncompleteFrame()
    {
        using MemoryStream stream = new([0x49, 0x54, 0x50, 0x01, 0x01]);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.IncompleteFrame, exception.ErrorCode);
        Assert.IsFalse(exception.IsPeerReported);
        Assert.IsFalse(exception.CanReplyWithTransportError);
    }

    [TestMethod]
    public async Task Read_TruncatedPayload_IsIncompleteFrame()
    {
        byte[] frame = ItpTestFrame.Build(ItpPacketType.ApplicationData, "hello"u8);
        using MemoryStream stream = new(frame[..^2]);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.IncompleteFrame, exception.ErrorCode);
        Assert.IsTrue(exception.CanReplyWithTransportError);
    }

    [TestMethod]
    public async Task Read_BadMagic_IsInvalidMagic()
    {
        byte[] preamble = [(byte)'X', (byte)'T', (byte)'P', ItpConstants.Version];
        using MemoryStream stream = new(preamble);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.InvalidMagic, exception.ErrorCode);
        Assert.IsFalse(exception.CanReplyWithTransportError);
    }

    [TestMethod]
    public async Task Read_UnsupportedVersion_RequiresOnlyStablePreamble()
    {
        byte[] preamble = [(byte)'I', (byte)'T', (byte)'P', 0x7F];
        using MemoryStream stream = new(preamble);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());

        Assert.AreEqual(ItpErrorCode.VersionMismatch, exception.ErrorCode);
        Assert.IsFalse(exception.CanReplyWithTransportError);
        Assert.AreEqual(ItpConstants.PreambleSize, stream.Position);
    }

    [TestMethod]
    public async Task Read_UnknownPacketType_IsUnsupportedPacketType()
    {
        byte[] frame = ItpTestFrame.Build((ItpPacketType)0x7F, "hello"u8);
        using MemoryStream stream = new(frame);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.UnsupportedPacketType, exception.ErrorCode);
        Assert.IsTrue(exception.CanReplyWithTransportError);
        Assert.AreEqual(ItpConstants.Version1HeaderSize, stream.Position);
    }

    [TestMethod]
    public async Task Read_UnsupportedApplicationPayloadFormat_IsRejectedBeforeBodyRead()
    {
        byte[] frame = ItpTestFrame.Build(
            ItpPacketType.ApplicationData,
            "hello"u8,
            payloadFormat: (ItpPayloadFormat)0x7F);
        using MemoryStream stream = new(frame);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());

        Assert.AreEqual(ItpErrorCode.UnsupportedPayloadFormat, exception.ErrorCode);
        Assert.IsTrue(exception.CanReplyWithTransportError);
        Assert.AreEqual(ItpConstants.Version1HeaderSize, stream.Position);
    }

    [TestMethod]
    public async Task Read_TransportErrorWithApplicationFormat_IsInvalidFrame()
    {
        byte[] frame = ItpTestFrame.Build(
            ItpPacketType.TransportError,
            [0x00, 0x01, 0x00, 0x00],
            payloadFormat: ItpPayloadFormat.IpcJson);
        using MemoryStream stream = new(frame);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());

        Assert.AreEqual(ItpErrorCode.InvalidFrame, exception.ErrorCode);
        Assert.IsFalse(exception.CanReplyWithTransportError);
        Assert.AreEqual(ItpConstants.Version1HeaderSize, stream.Position);
    }

    [TestMethod]
    public async Task RoundTrip_PayloadAtConfiguredMaximum_IsAccepted()
    {
        ItpOptions options = new() { MaxPayloadLength = 16 };
        byte[] payload = Enumerable.Range(0, options.MaxPayloadLength).Select(static value => (byte)value).ToArray();
        using MemoryStream stream = new();

        await new ItpConnection(stream, options).WriteApplicationDataAsync(payload);

        stream.Position = 0;
        ItpFrame frame = await new ItpConnection(stream, options).ReadAsync();

        CollectionAssert.AreEqual(payload, frame.Payload.ToArray());
    }

    [TestMethod]
    public async Task Read_DeclaredLengthExceedsLimit_IsPayloadTooLarge_AndDoesNotReadBody()
    {
        byte[] header = new byte[ItpConstants.Version1HeaderSize];
        "ITP"u8.CopyTo(header);
        header[3] = ItpConstants.Version;
        header[4] = (byte)ItpPacketType.ApplicationData;
        header[5] = (byte)ItpPayloadFormat.IpcJson;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(6, 4), 1024);

        using MemoryStream stream = new(header);
        ItpOptions options = new() { MaxPayloadLength = 16 };
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream, options).ReadAsync());
        Assert.AreEqual(ItpErrorCode.PayloadTooLarge, exception.ErrorCode);
        Assert.IsTrue(exception.CanReplyWithTransportError);
        Assert.AreEqual(header.Length, stream.Position);
    }

    [TestMethod]
    public async Task Read_EmptyApplicationData_IsEmptyApplicationPayload()
    {
        byte[] frame = ItpTestFrame.Build(ItpPacketType.ApplicationData, []);
        using MemoryStream stream = new(frame);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.EmptyApplicationPayload, exception.ErrorCode);
        Assert.IsTrue(exception.CanReplyWithTransportError);
        Assert.AreEqual(ItpConstants.Version1HeaderSize, stream.Position);
    }

    [TestMethod]
    public async Task Read_MalformedTransportErrorPayload_IsInvalidFrame()
    {
        byte[] frame = ItpTestFrame.Build(
            ItpPacketType.TransportError,
            [0x00, 0x02],
            payloadFormat: ItpPayloadFormat.None);
        using MemoryStream stream = new(frame);
        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());
        Assert.AreEqual(ItpErrorCode.InvalidFrame, exception.ErrorCode);
        Assert.IsFalse(exception.IsPeerReported);
        Assert.IsFalse(exception.CanReplyWithTransportError);
    }

    [TestMethod]
    public async Task Read_TransportErrorWithInvalidUtf8_IsInvalidFrame()
    {
        byte[] frame = ItpTestFrame.Build(
            ItpPacketType.TransportError,
            [0x00, 0x07, 0x00, 0x01, 0xFF],
            payloadFormat: ItpPayloadFormat.None);
        using MemoryStream stream = new(frame);

        ItpException exception = await Assert.ThrowsExactlyAsync<ItpException>(async () =>
            await new ItpConnection(stream).ReadAsync());

        Assert.AreEqual(ItpErrorCode.InvalidFrame, exception.ErrorCode);
        Assert.IsFalse(exception.IsPeerReported);
        Assert.IsFalse(exception.CanReplyWithTransportError);
    }

    [TestMethod]
    public async Task Cancellation_AbortsRead()
    {
        Pipe pipe = new();
        await using Stream stream = pipe.Reader.AsStream();
        using CancellationTokenSource cts = new();
        Task read = new ItpConnection(stream).ReadAsync(cts.Token).AsTask();
        await cts.CancelAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await read);
    }

    [TestMethod]
    public async Task TimedStream_ReadTimeout_DoesNotWaitForever()
    {
        Pipe pipe = new();
        await using Stream inner = pipe.Reader.AsStream();
        await using TimedStream timed = new(inner, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsExactlyAsync<TimeoutException>(async () =>
            await new ItpConnection(timed).ReadAsync());
    }
}

file sealed class OneByteReadStream(byte[] data) : Stream
{
    private int _offset;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => data.Length;

    public override long Position
    {
        get => _offset;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_offset >= data.Length || count <= 0)
        {
            return 0;
        }

        buffer[offset] = data[_offset];
        _offset++;
        return 1;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (buffer.IsEmpty || _offset >= data.Length)
        {
            return ValueTask.FromResult(0);
        }

        buffer.Span[0] = data[_offset];
        _offset++;
        return ValueTask.FromResult(1);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

file static class ItpTestFrame
{
    public static byte[] Build(
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
