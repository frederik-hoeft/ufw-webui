namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Inclusive range of a discrete numeric axis.
/// </summary>
public readonly record struct Interval<T> where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
{
    /// <summary>
    /// Creates a range. <paramref name="start"/> and <paramref name="end"/> are both included.
    /// </summary>
    public Interval(T start, T end)
    {
        if (start > end)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "Interval end is before start.");
        }

        Start = start;
        End = end;
    }

    /// <summary>Gets the first included value.</summary>
    public T Start { get; }

    /// <summary>Gets the last included value.</summary>
    public T End { get; }

    /// <summary>Returns <see langword="true"/> when <paramref name="value"/> lies inside the range.</summary>
    public bool Contains(T value) => value >= Start && value <= End;

    /// <inheritdoc />
    public override string ToString() => $"[{Start}, {End}]";
}
