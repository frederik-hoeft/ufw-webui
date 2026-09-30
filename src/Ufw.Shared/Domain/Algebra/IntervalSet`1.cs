using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Canonical union of non-empty, non-adjacent, non-overlapping <see cref="Interval{T}"/> values.
/// </summary>
public readonly struct IntervalSet<T> : IDimensionSet, IEquatable<IntervalSet<T>>
    where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>, IMinMaxValue<T>
{
    private static readonly IReadOnlyList<Interval<T>> s_empty = [];

    private readonly Interval<T>[]? _intervals;

    private IntervalSet(Interval<T>[] intervals)
    {
        _intervals = intervals.Length == 0 ? null : intervals;
    }

    /// <summary>Gets the empty set.</summary>
    public static IntervalSet<T> Empty => default;

    /// <summary>Gets a value indicating whether the set contains no values.</summary>
    public bool IsEmpty => _intervals is null;

    /// <summary>Gets the normalized intervals in ascending order.</summary>
    public IReadOnlyList<Interval<T>> Intervals => _intervals ?? s_empty;

    /// <summary>Gets the number of discrete values in the set.</summary>
    public BigInteger Cardinality
    {
        get
        {
            if (_intervals is null)
            {
                return BigInteger.Zero;
            }

            BigInteger total = BigInteger.Zero;
            foreach (Interval<T> interval in _intervals)
            {
                total += IntegerCardinality.Length(interval.Start, interval.End);
            }

            return total;
        }
    }

    /// <summary>Creates a set containing one range.</summary>
    public static IntervalSet<T> Of(Interval<T> interval) => new([interval]);

    /// <summary>Creates a set containing <paramref name="start"/> through <paramref name="end"/>, inclusive.</summary>
    public static IntervalSet<T> Between(T start, T end) => Of(new Interval<T>(start, end));

    /// <summary>Creates a set containing a single value.</summary>
    public static IntervalSet<T> Singleton(T value) => Between(value, value);

    /// <summary>Creates a normalized set from ranges that may overlap, adjoin, or arrive unsorted.</summary>
    public static IntervalSet<T> Of(IEnumerable<Interval<T>> intervals)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        List<Interval<T>> ordered = intervals.ToList();
        if (ordered.Count == 0)
        {
            return Empty;
        }

        ordered.Sort(static (left, right) =>
        {
            int compared = left.Start.CompareTo(right.Start);
            return compared != 0 ? compared : left.End.CompareTo(right.End);
        });

        List<Interval<T>> merged = [];
        foreach (Interval<T> interval in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(interval);
                continue;
            }

            Interval<T> current = merged[^1];
            if (OverlapsOrAdjacent(current, interval))
            {
                T end = current.End >= interval.End ? current.End : interval.End;
                merged[^1] = new Interval<T>(current.Start, end);
            }
            else
            {
                merged.Add(interval);
            }
        }

        return new IntervalSet<T>([.. merged]);
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="value"/> belongs to some interval.</summary>
    public bool Contains(T value)
    {
        if (_intervals is null)
        {
            return false;
        }

        int low = 0;
        int high = _intervals.Length - 1;
        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            Interval<T> interval = _intervals[mid];
            if (value < interval.Start)
            {
                high = mid - 1;
            }
            else if (value > interval.End)
            {
                low = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the intersection.</summary>
    public IntervalSet<T> Intersect(IntervalSet<T> other)
    {
        if (_intervals is null || other._intervals is null)
        {
            return Empty;
        }

        List<Interval<T>> result = [];
        int index = 0;
        int otherIndex = 0;
        while (index < _intervals.Length && otherIndex < other._intervals.Length)
        {
            Interval<T> left = _intervals[index];
            Interval<T> right = other._intervals[otherIndex];
            T start = left.Start > right.Start ? left.Start : right.Start;
            T end = left.End < right.End ? left.End : right.End;
            if (start <= end)
            {
                result.Add(new Interval<T>(start, end));
            }

            if (left.End < right.End)
            {
                index++;
            }
            else
            {
                otherIndex++;
            }
        }

        return new IntervalSet<T>([.. result]);
    }

    /// <summary>Returns the union.</summary>
    public IntervalSet<T> Union(IntervalSet<T> other)
    {
        if (_intervals is null)
        {
            return other;
        }

        if (other._intervals is null)
        {
            return this;
        }

        Interval<T>[] combined = new Interval<T>[_intervals.Length + other._intervals.Length];
        _intervals.CopyTo(combined, 0);
        other._intervals.CopyTo(combined, _intervals.Length);
        return Of(combined);
    }

    /// <summary>Returns <c>this \ other</c>.</summary>
    public IntervalSet<T> Except(IntervalSet<T> other)
    {
        if (_intervals is null || other._intervals is null)
        {
            return this;
        }

        List<Interval<T>> result = [];
        int cutIndex = 0;
        Interval<T>[] cuts = other._intervals;
        foreach (Interval<T> source in _intervals)
        {
            T start = source.Start;
            T end = source.End;
            while (cutIndex < cuts.Length && cuts[cutIndex].End < start)
            {
                cutIndex++;
            }

            bool consumed = false;
            int cursor = cutIndex;
            while (cursor < cuts.Length && cuts[cursor].Start <= end)
            {
                Interval<T> cut = cuts[cursor];
                if (cut.Start > start)
                {
                    result.Add(new Interval<T>(start, Predecessor(cut.Start)));
                }

                if (cut.End >= end)
                {
                    consumed = true;
                    break;
                }

                start = Successor(cut.End);
                cursor++;
            }

            if (!consumed)
            {
                result.Add(new Interval<T>(start, end));
            }
        }

        return new IntervalSet<T>([.. result]);
    }

    /// <summary>Returns <see langword="true"/> when the intersection is non-empty.</summary>
    public bool Overlaps(IntervalSet<T> other) => !Intersect(other).IsEmpty;

    /// <summary>Returns <see langword="true"/> when every value of <paramref name="other"/> is contained.</summary>
    public bool IsSupersetOf(IntervalSet<T> other) => other.Except(this).IsEmpty;

    /// <inheritdoc />
    public int CompareTo(IntervalSet<T> other)
    {
        int count = _intervals?.Length ?? 0;
        int otherCount = other._intervals?.Length ?? 0;
        int shared = Math.Min(count, otherCount);
        for (int index = 0; index < shared; index++)
        {
            Interval<T> left = _intervals![index];
            Interval<T> right = other._intervals![index];
            int compared = left.Start.CompareTo(right.Start);
            if (compared != 0)
            {
                return compared;
            }

            compared = left.End.CompareTo(right.End);
            if (compared != 0)
            {
                return compared;
            }
        }

        return count.CompareTo(otherCount);
    }

    /// <inheritdoc />
    public bool Equals(IntervalSet<T> other) => CompareTo(other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is IntervalSet<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        if (_intervals is null)
        {
            return 0;
        }

        hash.Add(_intervals.Length);
        foreach (Interval<T> interval in _intervals)
        {
            hash.Add(interval.Start);
            hash.Add(interval.End);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (_intervals is null)
        {
            return "{}";
        }

        return "{" + string.Join(", ", _intervals) + "}";
    }

    int IDimensionSet.CompareTo(IDimensionSet other) => CompareTo(Expect(other));

    bool IDimensionSet.SetEquals(IDimensionSet other) => Equals(Expect(other));

    bool IDimensionSet.Overlaps(IDimensionSet other) => Overlaps(Expect(other));

    bool IDimensionSet.Contains(IDimensionSet other) => IsSupersetOf(Expect(other));

    IDimensionSet IDimensionSet.Intersect(IDimensionSet other) => Intersect(Expect(other));

    IDimensionSet IDimensionSet.Except(IDimensionSet other) => Except(Expect(other));

    IDimensionSet IDimensionSet.Union(IDimensionSet other) => Union(Expect(other));

    private static IntervalSet<T> Expect(IDimensionSet other)
    {
        if (other is IntervalSet<T> typed)
        {
            return typed;
        }

        throw new ArgumentException("Interval sets can only be combined with interval sets of the same value type.", nameof(other));
    }

    private static bool OverlapsOrAdjacent(Interval<T> current, Interval<T> next)
    {
        if (current.End == T.MaxValue || next.Start <= current.End)
        {
            return true;
        }

        return next.Start <= current.End + T.One;
    }

    private static T Predecessor(T value)
    {
        if (value == T.Zero)
        {
            throw new InvalidOperationException("Cannot address the predecessor of the minimum integer.");
        }

        return value - T.One;
    }

    private static T Successor(T value)
    {
        if (value == T.MaxValue)
        {
            throw new InvalidOperationException("Cannot address the successor of the maximum integer.");
        }

        return value + T.One;
    }
}
