using System.Buffers.Binary;
using System.Text;

namespace Ufw.Ipc.Shared.Transport.Itp;

internal static class ItpTransportErrorPayload
{
    public const int HEADER_SIZE = 4;

    public static byte[] Encode(ItpErrorCode errorCode, string? message)
    {
        ReadOnlySpan<byte> utf8 = message is { Length: > 0 } ? Encoding.UTF8.GetBytes(message) : [];
        if (utf8.Length > ushort.MaxValue)
        {
            utf8 = utf8[..ushort.MaxValue];
        }

        byte[] buffer = new byte[HEADER_SIZE + utf8.Length];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), (ushort)errorCode);
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), (ushort)utf8.Length);
        utf8.CopyTo(buffer.AsSpan(HEADER_SIZE));
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
        if (payload.Length != HEADER_SIZE + messageLength)
        {
            throw ItpException.Local(
                ItpErrorCode.InvalidFrame,
                $"Transport error message length {messageLength} does not match remaining payload {payload.Length - HEADER_SIZE}.");
        }

        string message = messageLength == 0
            ? string.Empty
            : Encoding.UTF8.GetString(payload.Slice(HEADER_SIZE, messageLength));
        return ((ItpErrorCode)code, message);
    }
}
