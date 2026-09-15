namespace Ufw.Shared.Web;

/// <summary>
/// Represents a heap-backed URI route or query key-value pair.
/// </summary>
public readonly struct RouteData
{
    /// <summary>
    /// Gets the route-data key.
    /// </summary>
    public readonly string Key;

    /// <summary>
    /// Gets the route-data value.
    /// </summary>
    public readonly string Value;

    private RouteData(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Creates route data from a string key and value.
    /// </summary>
    public static RouteData Create(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new RouteData(key, value ?? string.Empty);
    }

    /// <summary>
    /// Creates route data by formatting <paramref name="value"/> with its normal string representation.
    /// </summary>
    public static RouteData Create<TValue>(string key, TValue? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new RouteData(key, value?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Creates a span-backed view over this route data.
    /// </summary>
    public static implicit operator RouteDataRef(RouteData routeData) => RouteDataRef.Create(routeData.Key, routeData.Value);
}
