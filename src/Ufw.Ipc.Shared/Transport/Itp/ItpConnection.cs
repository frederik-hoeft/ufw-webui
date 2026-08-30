using System.Buffers;
using System.Buffers.Binary;

namespace Ufw.Ipc.Shared.Transport.Itp;

/// <summary>
/// Reads and writes ITP frames on an already-opened stream. Does not own the stream.
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
        byte[] preambleBuffer = ArrayPool<byte>.Shared.Rent(ItpConstants.PreambleSize);
        try
        {
            Memory<byte> preamble = preambleBuffer.AsMemory(0, ItpConstants.PreambleSize);
            await ReadExactAsync(_stream, preamble, cancellationToken).ConfigureAwait(false);
            byte version = ParsePreamble(preamble.Span);

            return version switch
            {
                ItpConstants.Version => await ReadVersion1Async(cancellationToken).ConfigureAwait(false),
                _ => throw ItpException.Local(
                    ItpErrorCode.VersionMismatch,
                    $"Unsupported ITP version {version}; this peer speaks version {ItpConstants.Version}."),
            };
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(preambleBuffer);
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

        return WriteVersion1FrameAsync(
            ItpPacketType.ApplicationData,
            ItpPayloadFormat.IpcJson,
            payload,
            cancellationToken);
    }

    public ValueTask WriteTransportErrorAsync(
        ItpErrorCode errorCode,
        string? message,
        CancellationToken cancellationToken = default)
    {
        byte[] payload = ItpTransportErrorPayload.Encode(errorCode, message);
        return WriteVersion1FrameAsync(
            ItpPacketType.TransportError,
            ItpPayloadFormat.None,
            payload,
            cancellationToken);
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
        catch (IOException)
        {
            // The stream is already unusable; the caller is about to close it.
        }
        catch (ObjectDisposedException)
        {
            // The stream is already unusable; the caller is about to close it.
        }
        catch (NotSupportedException)
        {
            // The stream cannot carry a reply; the caller is about to close it.
        }
        catch (ItpException)
        {
            // The generated error frame itself could not satisfy the local ITP constraints.
        }
    }

    private async ValueTask<ItpFrame> ReadVersion1Async(CancellationToken cancellationToken)
    {
        byte[] headerBuffer = ArrayPool<byte>.Shared.Rent(ItpConstants.Version1HeaderRemainderSize);
        try
        {
            Memory<byte> header = headerBuffer.AsMemory(0, ItpConstants.Version1HeaderRemainderSize);
            await ReadExactAsync(_stream, header, cancellationToken).ConfigureAwait(false);

            ParseVersion1Header(
                header.Span,
                out ItpPacketType packetType,
                out ItpPayloadFormat payloadFormat,
                out int payloadLength);

            byte[] payload = new byte[payloadLength];
            await ReadExactAsync(
                _stream,
                payload,
                cancellationToken,
                canReplyWithTransportError: packetType == ItpPacketType.ApplicationData).ConfigureAwait(false);

            if (packetType == ItpPacketType.TransportError)
            {
                (ItpErrorCode peerCode, string peerMessage) = ItpTransportErrorPayload.Decode(payload);
                string detail = string.IsNullOrEmpty(peerMessage)
                    ? $"Peer reported ITP error {peerCode}."
                    : $"Peer reported ITP error {peerCode}: {peerMessage}";
                throw ItpException.PeerReported(peerCode, detail);
            }

            return new ItpFrame(packetType, payloadFormat, payload);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuffer);
        }
    }

    private async ValueTask WriteVersion1FrameAsync(
        ItpPacketType packetType,
        ItpPayloadFormat payloadFormat,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (payload.Length > _options.MaxPayloadLength)
        {
            throw ItpException.Local(
                ItpErrorCode.PayloadTooLarge,
                $"Refusing to write a payload of {payload.Length} bytes; maximum is {_options.MaxPayloadLength}.");
        }

        ValidateVersion1Packet(packetType, payloadFormat, payload.Length);

        int frameLength = ItpConstants.Version1HeaderSize + payload.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(frameLength);
        try
        {
            Span<byte> frame = buffer.AsSpan(0, frameLength);
            ItpConstants.Magic.CopyTo(frame);
            frame[3] = ItpConstants.Version;
            frame[4] = (byte)packetType;
            frame[5] = (byte)payloadFormat;
            BinaryPrimitives.WriteUInt32BigEndian(frame.Slice(6, 4), (uint)payload.Length);
            payload.Span.CopyTo(frame[ItpConstants.Version1HeaderSize..]);

            await _stream.WriteAsync(buffer.AsMemory(0, frameLength), cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte ParsePreamble(ReadOnlySpan<byte> preamble)
    {
        if (!preamble[..ItpConstants.MagicSize].SequenceEqual(ItpConstants.Magic))
        {
            throw ItpException.Local(ItpErrorCode.InvalidMagic, "Frame magic is not 'ITP'.");
        }

        return preamble[ItpConstants.MagicSize];
    }

    private void ParseVersion1Header(
        ReadOnlySpan<byte> header,
        out ItpPacketType packetType,
        out ItpPayloadFormat payloadFormat,
        out int payloadLength)
    {
        byte rawType = header[0];
        byte rawPayloadFormat = header[1];
        uint declaredLength = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(2, 4));

        bool canReplyWithTransportError = rawType != (byte)ItpPacketType.TransportError;
        if (declaredLength > (uint)_options.MaxPayloadLength)
        {
            throw ItpException.Local(
                ItpErrorCode.PayloadTooLarge,
                $"Declared payload length {declaredLength} exceeds the maximum of {_options.MaxPayloadLength}.",
                canReplyWithTransportError);
        }

        packetType = rawType switch
        {
            (byte)ItpPacketType.ApplicationData => ItpPacketType.ApplicationData,
            (byte)ItpPacketType.TransportError => ItpPacketType.TransportError,
            _ => throw ItpException.Local(
                ItpErrorCode.UnsupportedPacketType,
                $"Unsupported ITP packet type 0x{rawType:X2}.",
                canReplyWithTransportError: true),
        };

        payloadFormat = (ItpPayloadFormat)rawPayloadFormat;
        payloadLength = (int)declaredLength;
        ValidateVersion1Packet(packetType, payloadFormat, payloadLength, canReplyWithTransportError);
    }

    private static void ValidateVersion1Packet(
        ItpPacketType packetType,
        ItpPayloadFormat payloadFormat,
        int payloadLength,
        bool canReplyWithTransportError = false)
    {
        if (packetType == ItpPacketType.ApplicationData)
        {
            if (payloadFormat != ItpPayloadFormat.IpcJson)
            {
                throw ItpException.Local(
                    ItpErrorCode.UnsupportedPayloadFormat,
                    $"Unsupported application payload format 0x{(byte)payloadFormat:X2}.",
                    canReplyWithTransportError);
            }

            if (payloadLength == 0)
            {
                throw ItpException.Local(
                    ItpErrorCode.EmptyApplicationPayload,
                    "ApplicationData frame has an empty payload.",
                    canReplyWithTransportError);
            }

            return;
        }

        if (payloadFormat != ItpPayloadFormat.None)
        {
            throw ItpException.Local(
                ItpErrorCode.InvalidFrame,
                "TransportError frames must use the None payload format.",
                canReplyWithTransportError);
        }
    }

    internal static async ValueTask ReadExactAsync(
        Stream stream,
        Memory<byte> destination,
        CancellationToken cancellationToken,
        bool canReplyWithTransportError = false)
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
                    $"Connection closed {where} while reading an ITP frame.",
                    canReplyWithTransportError);
            }

            offset += read;
        }
    }
}
