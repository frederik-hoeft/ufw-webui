using System.Buffers;
using System.Buffers.Binary;

namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// Reads and writes ITP v1 frames on an already-opened stream. Does not own the stream.
/// </summary>
public sealed class ItpConnection
{
    private readonly Stream _stream;
    private readonly ItpOptions _options;

    public ItpConnection(Stream stream, ItpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _options = options ?? ItpOptions.Default;
        if (_options.MaxPayloadLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxPayloadLength must be positive.");
        }
    }

    public async ValueTask<ItpFrame> ReadAsync(CancellationToken cancellationToken = default)
    {
        byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(ItpConstants.HeaderSize);
        try
        {
            await ReadExactAsync(
                _stream,
                headerBuffer.AsMemory(0, ItpConstants.HeaderSize),
                cancellationToken).ConfigureAwait(false);
            return await ReadBodyAsync(headerBuffer.AsMemory(0, ItpConstants.HeaderSize), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
        }
    }

    public ValueTask WriteApplicationDataAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (payload.Length == 0)
        {
            throw ItpException.Local(
                ItpErrorCode.EmptyApplicationPayload,
                "Refusing to write an ApplicationData frame with an empty payload.");
        }

        return WriteFrameAsync(ItpPacketType.ApplicationData, payload, cancellationToken);
    }

    public ValueTask WriteTransportErrorAsync(
        ItpErrorCode errorCode,
        string? message,
        CancellationToken cancellationToken = default)
    {
        byte[] payload = ItpTransportErrorPayload.Encode(errorCode, message);
        return WriteFrameAsync(ItpPacketType.TransportError, payload, cancellationToken);
    }

    public static async ValueTask TryWriteTransportErrorAsync(
        Stream stream,
        ItpOptions options,
        ItpErrorCode errorCode,
        string? message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            ItpConnection connection = new(stream, options);
            await connection.WriteTransportErrorAsync(errorCode, message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or NotSupportedException or ItpException)
        {
            // The stream is already unusable; the caller is about to close it.
        }
    }

    private async ValueTask<ItpFrame> ReadBodyAsync(ReadOnlyMemory<byte> headerMemory, CancellationToken cancellationToken)
    {
        ParseHeader(headerMemory.Span, out byte version, out byte rawType, out byte flags, out uint declaredLength);

        if (declaredLength > (uint)_options.MaxPayloadLength)
        {
            throw ItpException.Local(
                ItpErrorCode.PayloadTooLarge,
                $"Declared payload length {declaredLength} exceeds the maximum of {_options.MaxPayloadLength}.");
        }

        int payloadLength = (int)declaredLength;
        int tailLength = payloadLength + ItpConstants.TrailerSize;
        byte[] tail = ArrayPool<byte>.Shared.Rent(tailLength);
        try
        {
            await ReadExactAsync(_stream, tail.AsMemory(0, tailLength), cancellationToken).ConfigureAwait(false);

            uint expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(tail.AsSpan(payloadLength, ItpConstants.TrailerSize));
            uint actualCrc = ItpCrc32.Append(ItpCrc32.Compute(headerMemory.Span), tail.AsSpan(0, payloadLength));
            if (expectedCrc != actualCrc)
            {
                throw ItpException.Local(
                    ItpErrorCode.InvalidChecksum,
                    "Frame CRC-32 does not match the header and payload.");
            }

            if (version != ItpConstants.Version)
            {
                throw ItpException.Local(
                    ItpErrorCode.VersionMismatch,
                    $"Unsupported ITP version {version}; this peer speaks version {ItpConstants.Version}.");
            }

            if (flags != 0)
            {
                throw ItpException.Local(
                    ItpErrorCode.UnsupportedFlags,
                    $"Unsupported ITP flags 0x{flags:X2}; v1 requires flags to be 0.");
            }

            if (rawType is not ((byte)ItpPacketType.ApplicationData) and not ((byte)ItpPacketType.TransportError))
            {
                throw ItpException.Local(
                    ItpErrorCode.UnsupportedPacketType,
                    $"Unsupported ITP packet type 0x{rawType:X2}.");
            }

            ItpPacketType packetType = (ItpPacketType)rawType;
            byte[] payloadCopy = payloadLength == 0 ? [] : tail.AsSpan(0, payloadLength).ToArray();

            if (packetType == ItpPacketType.ApplicationData && payloadCopy.Length == 0)
            {
                throw ItpException.Local(
                    ItpErrorCode.EmptyApplicationPayload,
                    "ApplicationData frame has an empty payload.");
            }

            if (packetType == ItpPacketType.TransportError)
            {
                (ItpErrorCode peerCode, string peerMessage) = ItpTransportErrorPayload.Decode(payloadCopy);
                string detail = string.IsNullOrEmpty(peerMessage)
                    ? $"Peer reported ITP error {peerCode}."
                    : $"Peer reported ITP error {peerCode}: {peerMessage}";
                throw ItpException.PeerReported(peerCode, detail);
            }

            return new ItpFrame(packetType, payloadCopy);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tail);
        }
    }

    private async ValueTask WriteFrameAsync(
        ItpPacketType packetType,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length > _options.MaxPayloadLength)
        {
            throw ItpException.Local(
                ItpErrorCode.PayloadTooLarge,
                $"Refusing to write a payload of {payload.Length} bytes; maximum is {_options.MaxPayloadLength}.");
        }

        int frameLength = ItpConstants.HeaderSize + payload.Length + ItpConstants.TrailerSize;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(frameLength);
        try
        {
            Span<byte> frame = buffer.AsSpan(0, frameLength);
            ItpConstants.Magic.CopyTo(frame);
            frame[3] = ItpConstants.Version;
            frame[4] = (byte)packetType;
            frame[5] = 0;
            BinaryPrimitives.WriteUInt32BigEndian(frame.Slice(6, 4), (uint)payload.Length);
            payload.Span.CopyTo(frame[ItpConstants.HeaderSize..]);
            uint crc = ItpCrc32.Compute(frame[..^ItpConstants.TrailerSize]);
            BinaryPrimitives.WriteUInt32BigEndian(frame[^ItpConstants.TrailerSize..], crc);

            await _stream.WriteAsync(buffer.AsMemory(0, frameLength), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ParseHeader(ReadOnlySpan<byte> header, out byte version, out byte rawType, out byte flags, out uint declaredLength)
    {
        if (!header[..ItpConstants.MagicSize].SequenceEqual(ItpConstants.Magic))
        {
            throw ItpException.Local(ItpErrorCode.InvalidMagic, "Frame magic is not 'ITP'.");
        }

        version = header[3];
        rawType = header[4];
        flags = header[5];
        declaredLength = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(6, 4));
    }

    internal static async ValueTask ReadExactAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = await stream.ReadAsync(destination[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                string where = offset == 0
                    ? "before any of the expected bytes arrived"
                    : $"after {offset} of {destination.Length} expected bytes";
                throw ItpException.Local(
                    ItpErrorCode.IncompleteFrame,
                    $"Connection closed {where} while reading an ITP frame.");
            }

            offset += read;
        }
    }
}
