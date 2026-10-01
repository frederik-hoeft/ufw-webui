using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Converts unsigned generic integers to exact cardinalities without wrapping at the source integer width.
/// </summary>
internal static class IntegerCardinality
{
    public static BigInteger ToBigInteger<T>(T value)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T> =>
        BigInteger.CreateChecked(value);

    public static BigInteger Length<T>(T start, T end)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
    {
        if (start > end)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Interval end is before start.");
        }

        return ToBigInteger(end) - ToBigInteger(start) + BigInteger.One;
    }
}
