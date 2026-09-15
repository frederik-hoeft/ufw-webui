namespace Ufw.Shared.Web;

/// <summary>
/// Represents a span-backed URI route or query key-value pair.
/// </summary>
public readonly ref struct RouteDataRef
{
    /// <summary>
    /// Gets the route-data key.
    /// </summary>
    public readonly ReadOnlySpan<char> Key;

    /// <summary>
    /// Gets the route-data value.
    /// </summary>
    public readonly ReadOnlySpan<char> Value;

    private RouteDataRef(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Creates an independent heap-backed copy of this route data.
    /// </summary>
    public RouteData CreateDeepCopy() => RouteData.Create(Key.ToString(), Value.ToString());

    /// <summary>
    /// Creates a span-backed route-data view.
    /// </summary>
    public static RouteDataRef Create(ReadOnlySpan<char> key, ReadOnlySpan<char> value) => new(key, value);

    /// <summary>
    /// Creates span-backed route data by formatting <paramref name="value"/> with its normal string representation.
    /// </summary>
    public static RouteDataRef Create<TValue>(ReadOnlySpan<char> key, TValue? value) => new(key, value?.ToString() ?? string.Empty);

    /// <summary>
    /// Creates an independent heap-backed copy of <paramref name="routeData"/>.
    /// </summary>
    public static explicit operator RouteData(RouteDataRef routeData) => routeData.CreateDeepCopy();
}
