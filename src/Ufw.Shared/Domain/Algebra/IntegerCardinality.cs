using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Converts generic binary integers to non-negative cardinalities without wrapping at the integer width.
/// </summary>
internal static class IntegerCardinality
{
    public static BigInteger ToBigInteger<T>(T value)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        // 32 bytes covers every BCL binary integer through UInt128. A wider value fails the write below.
        Span<byte> buffer = stackalloc byte[32];
        if (!value.TryWriteLittleEndian(buffer, out int written))
        {
            throw new InvalidOperationException("Unable to convert the integer to a cardinality.");
        }

        if (written <= 0)
        {
            return BigInteger.Zero;
        }

        return new BigInteger(buffer[..written], isUnsigned: true, isBigEndian: false);
    }

    public static BigInteger Length<T>(T start, T end)
        where T : struct, IBinaryInteger<T>, IMinMaxValue<T>
    {
        if (start > end)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Interval end is before start.");
        }

        return ToBigInteger(end) - ToBigInteger(start) + BigInteger.One;
    }
}
