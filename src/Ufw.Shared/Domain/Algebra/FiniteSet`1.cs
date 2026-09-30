using System.Numerics;

namespace Ufw.Shared.Domain.Algebra;

/// <summary>
/// Sorted set of discrete symbols. Equality and order use <see cref="IComparable{T}"/>.
/// </summary>
public readonly struct FiniteSet<T> : IDimensionSet, IEquatable<FiniteSet<T>>
    where T : IEquatable<T>, IComparable<T>
{
    private static readonly IReadOnlyList<T> s_empty = [];

    private readonly T[]? _values;

    private FiniteSet(T[] values)
    {
        _values = values.Length == 0 ? null : values;
    }

    /// <summary>Gets the empty set.</summary>
    public static FiniteSet<T> Empty => default;

    /// <summary>Gets a value indicating whether the set contains no values.</summary>
    public bool IsEmpty => _values is null;

    /// <summary>Gets the values in ascending order.</summary>
    public IReadOnlyList<T> Values => _values ?? s_empty;

    /// <summary>Gets the number of values.</summary>
    public BigInteger Cardinality => _values?.Length ?? 0;

    /// <summary>Creates a set containing the supplied values. Duplicates are ignored.</summary>
    public static FiniteSet<T> Of(params T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return From(values);
    }

    /// <summary>Creates a set containing the supplied values. Duplicates are ignored.</summary>
    public static FiniteSet<T> From(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        List<T> ordered = values.ToList();
        if (ordered.Count == 0)
        {
            return Empty;
        }

        ordered.Sort();
        int distinct = 0;
        for (int index = 0; index < ordered.Count; index++)
        {
            if (distinct == 0 || !ordered[distinct - 1].Equals(ordered[index]))
            {
                ordered[distinct] = ordered[index];
                distinct++;
            }
        }

        if (distinct != ordered.Count)
        {
            ordered.RemoveRange(distinct, ordered.Count - distinct);
        }

        return new FiniteSet<T>([.. ordered]);
    }

    /// <summary>Returns <see langword="true"/> when <paramref name="value"/> is a member.</summary>
    public bool Contains(T value)
    {
        if (_values is null)
        {
            return false;
        }

        return Array.BinarySearch(_values, value) >= 0;
    }

    /// <summary>Returns the intersection.</summary>
    public FiniteSet<T> Intersect(FiniteSet<T> other)
    {
        if (_values is null || other._values is null)
        {
            return Empty;
        }

        List<T> result = [];
        int index = 0;
        int otherIndex = 0;
        while (index < _values.Length && otherIndex < other._values.Length)
        {
            int compared = _values[index].CompareTo(other._values[otherIndex]);
            if (compared == 0)
            {
                result.Add(_values[index]);
                index++;
                otherIndex++;
            }
            else if (compared < 0)
            {
                index++;
            }
            else
            {
                otherIndex++;
            }
        }

        return new FiniteSet<T>([.. result]);
    }

    /// <summary>Returns the union.</summary>
    public FiniteSet<T> Union(FiniteSet<T> other)
    {
        if (_values is null)
        {
            return other;
        }

        if (other._values is null)
        {
            return this;
        }

        List<T> result = new(_values.Length + other._values.Length);
        int index = 0;
        int otherIndex = 0;
        while (index < _values.Length && otherIndex < other._values.Length)
        {
            int compared = _values[index].CompareTo(other._values[otherIndex]);
            if (compared == 0)
            {
                result.Add(_values[index]);
                index++;
                otherIndex++;
            }
            else if (compared < 0)
            {
                result.Add(_values[index]);
                index++;
            }
            else
            {
                result.Add(other._values[otherIndex]);
                otherIndex++;
            }
        }

        while (index < _values.Length)
        {
            result.Add(_values[index]);
            index++;
        }

        while (otherIndex < other._values.Length)
        {
            result.Add(other._values[otherIndex]);
            otherIndex++;
        }

        return new FiniteSet<T>([.. result]);
    }

    /// <summary>Returns <c>this \ other</c>.</summary>
    public FiniteSet<T> Except(FiniteSet<T> other)
    {
        if (_values is null || other._values is null)
        {
            return this;
        }

        List<T> result = [];
        int index = 0;
        int otherIndex = 0;
        while (index < _values.Length && otherIndex < other._values.Length)
        {
            int compared = _values[index].CompareTo(other._values[otherIndex]);
            if (compared == 0)
            {
                index++;
                otherIndex++;
            }
            else if (compared < 0)
            {
                result.Add(_values[index]);
                index++;
            }
            else
            {
                otherIndex++;
            }
        }

        while (index < _values.Length)
        {
            result.Add(_values[index]);
            index++;
        }

        return new FiniteSet<T>([.. result]);
    }

    /// <summary>Returns <see langword="true"/> when the intersection is non-empty.</summary>
    public bool Overlaps(FiniteSet<T> other) => !Intersect(other).IsEmpty;

    /// <summary>Returns <see langword="true"/> when every value of <paramref name="other"/> is a member.</summary>
    public bool IsSupersetOf(FiniteSet<T> other) => other.Except(this).IsEmpty;

    /// <inheritdoc />
    public int CompareTo(FiniteSet<T> other)
    {
        int count = _values?.Length ?? 0;
        int otherCount = other._values?.Length ?? 0;
        int shared = Math.Min(count, otherCount);
        for (int index = 0; index < shared; index++)
        {
            int compared = _values![index].CompareTo(other._values![index]);
            if (compared != 0)
            {
                return compared;
            }
        }

        return count.CompareTo(otherCount);
    }

    /// <inheritdoc />
    public bool Equals(FiniteSet<T> other) => CompareTo(other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is FiniteSet<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        if (_values is null)
        {
            return 0;
        }

        hash.Add(_values.Length);
        foreach (T value in _values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (_values is null)
        {
            return "{}";
        }

        return "{" + string.Join(", ", _values) + "}";
    }

    int IDimensionSet.CompareTo(IDimensionSet other) => CompareTo(Expect(other));

    bool IDimensionSet.SetEquals(IDimensionSet other) => Equals(Expect(other));

    bool IDimensionSet.Overlaps(IDimensionSet other) => Overlaps(Expect(other));

    bool IDimensionSet.Contains(IDimensionSet other) => IsSupersetOf(Expect(other));

    IDimensionSet IDimensionSet.Intersect(IDimensionSet other) => Intersect(Expect(other));

    IDimensionSet IDimensionSet.Except(IDimensionSet other) => Except(Expect(other));

    IDimensionSet IDimensionSet.Union(IDimensionSet other) => Union(Expect(other));

    private static FiniteSet<T> Expect(IDimensionSet other)
    {
        if (other is FiniteSet<T> typed)
        {
            return typed;
        }

        throw new ArgumentException("Finite sets can only be combined with finite sets of the same value type.", nameof(other));
    }
}
