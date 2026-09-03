using System.Buffers.Binary;
using System.Text;

namespace Ufw.Ipc.Shared.Transport.Itp;

internal static class ItpTransportErrorPayload
{
    public const int HEADER_SIZE = 4;

    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static byte[] Encode(ItpErrorCode errorCode, string? message)
    {
        byte[] messageBuffer = new byte[ItpConstants.MAX_TRANSPORT_ERROR_MESSAGE_UTF_8_LENGTH];
        int bytesUsed = 0;
        if (!string.IsNullOrEmpty(message))
        {
            Encoder encoder = Encoding.UTF8.GetEncoder();
            encoder.Convert(message.AsSpan(), messageBuffer.AsSpan(), flush: true, out _, out bytesUsed, out _);
        }

        byte[] buffer = new byte[HEADER_SIZE + bytesUsed];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), (ushort)errorCode);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), (ushort)bytesUsed);
        messageBuffer.AsSpan(0, bytesUsed).CopyTo(buffer.AsSpan(HEADER_SIZE));
        return buffer;
    }

    public static (ItpErrorCode ErrorCode, string Message) Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HEADER_SIZE)
        {
            throw ItpException.Local(ItpErrorCode.InvalidFrame, "Transport error payload is shorter than 4 bytes.");
        }

        ushort code = BinaryPrimitives.ReadUInt16BigEndian(payload[..2]);
        ushort messageLength = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(2, 2));
        if (messageLength > ItpConstants.MAX_TRANSPORT_ERROR_MESSAGE_UTF_8_LENGTH)
        {
            throw ItpException.Local(ItpErrorCode.InvalidFrame,
                $"Transport error diagnostic length {messageLength} exceeds the v1 maximum of {ItpConstants.MAX_TRANSPORT_ERROR_MESSAGE_UTF_8_LENGTH} bytes.");
        }

        if (payload.Length != HEADER_SIZE + messageLength)
        {
            throw ItpException.Local(ItpErrorCode.InvalidFrame,
                $"Transport error message length {messageLength} does not match remaining payload {payload.Length - HEADER_SIZE}.");
        }

        string message;
        try
        {
            message = messageLength == 0
                ? string.Empty
                : s_strictUtf8.GetString(payload.Slice(HEADER_SIZE, messageLength));
        }
        catch (DecoderFallbackException ex)
        {
            throw new ItpException(ItpErrorCode.InvalidFrame, "Transport error diagnostic is not valid UTF-8.", ex);
        }

        return ((ItpErrorCode)code, message);
    }
}
