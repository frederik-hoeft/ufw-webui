using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Ufw.Shared.Web;

/// <summary>
/// Builds trusted relative or absolute URI strings from path and query components without requiring callers to concatenate URI syntax manually.
/// </summary>
[DebuggerDisplay("{Build(),nq}")]
public sealed class SimpleUriBuilder
{
    private const int DEFAULT_CAPACITY = 256;

    private readonly StringBuilder _builder;
    private bool _hasQuery;
    private bool _queryIsEmpty;

    private SimpleUriBuilder(StringBuilder builder, bool hasQuery, bool queryIsEmpty)
    {
        _builder = builder;
        _hasQuery = hasQuery;
        _queryIsEmpty = queryIsEmpty;
    }

    /// <summary>
    /// Creates a builder initialized from <paramref name="baseUri"/>.
    /// </summary>
    public static SimpleUriBuilder Create(ReadOnlySpan<char> baseUri, int capacity = DEFAULT_CAPACITY)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        StringBuilder builder = new(Math.Max(capacity, baseUri.Length));
        builder.Append(baseUri);

        int queryIndex = baseUri.IndexOf('?');
        bool hasQuery = queryIndex >= 0;
        bool queryIsEmpty = hasQuery && queryIndex == baseUri.Length - 1;
        return new SimpleUriBuilder(builder, hasQuery, queryIsEmpty);
    }

    /// <summary>
    /// Appends a path component, normalizing exactly one slash at the join boundary.
    /// </summary>
    /// <remarks>
    /// The supplied path is treated as trusted URI syntax and is not URL encoded.
    /// </remarks>
    public SimpleUriBuilder AppendPath(ReadOnlySpan<char> path)
    {
        if (_hasQuery)
        {
            throw new InvalidOperationException("A path cannot be appended after a query string has started.");
        }
        if (path.IsEmpty)
        {
            return this;
        }

        bool builderHasDelimiter = _builder.Length > 0 && _builder[^1] == '/';
        bool pathHasDelimiter = path[0] == '/';
        if (builderHasDelimiter && pathHasDelimiter)
        {
            _builder.Append(path[1..]);
        }
        else if (!builderHasDelimiter && !pathHasDelimiter)
        {
            _builder.Append('/').Append(path);
        }
        else
        {
            _builder.Append(path);
        }

        return this;
    }

    /// <summary>
    /// Appends a query parameter using the invariant string representation of <paramref name="value"/> when available.
    /// </summary>
    public SimpleUriBuilder AppendQuery<TValue>(ReadOnlySpan<char> key, TValue? value, bool urlEncode = true)
    {
        string formatted = value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };
        return AppendQuery(key, formatted.AsSpan(), urlEncode);
    }

    /// <summary>
    /// Appends a boolean query parameter as a lowercase URI value.
    /// </summary>
    public SimpleUriBuilder AppendQuery(ReadOnlySpan<char> key, bool value, bool urlEncode = true) => AppendQuery(key, (value ? "true" : "false").AsSpan(), urlEncode);

    /// <summary>
    /// Appends route data as a query parameter.
    /// </summary>
    public SimpleUriBuilder AppendQuery(RouteDataRef routeData, bool urlEncode = true) => AppendQuery(routeData.Key, routeData.Value, urlEncode);

    /// <summary>
    /// Appends route data as a query parameter.
    /// </summary>
    public SimpleUriBuilder AppendQuery(RouteData routeData, bool urlEncode = true) => AppendQuery(routeData.Key.AsSpan(), routeData.Value.AsSpan(), urlEncode);

    /// <summary>
    /// Appends a query parameter and URL-encodes its key and value by default.
    /// </summary>
    public SimpleUriBuilder AppendQuery(ReadOnlySpan<char> key, ReadOnlySpan<char> value, bool urlEncode = true)
    {
        if (key.IsEmpty || key.IsWhiteSpace())
        {
            throw new ArgumentException("Query parameter keys cannot be empty or whitespace.", nameof(key));
        }

        AppendQueryDelimiter();
        if (urlEncode)
        {
            _builder.Append(Uri.EscapeDataString(key));
            _builder.Append('=');
            _builder.Append(Uri.EscapeDataString(value));
        }
        else
        {
            _builder.Append(key);
            _builder.Append('=');
            _builder.Append(value);
        }

        _queryIsEmpty = false;
        return this;
    }

    /// <summary>
    /// Builds the URI string.
    /// </summary>
    public string Build() => _builder.ToString();

    /// <summary>
    /// Builds a <see cref="Uri"/> with the requested URI kind.
    /// </summary>
    public Uri BuildUri(UriKind uriKind) => new(Build(), uriKind);

    /// <inheritdoc />
    public override string ToString() => Build();

    private void AppendQueryDelimiter()
    {
        if (!_hasQuery)
        {
            _builder.Append('?');
            _hasQuery = true;
            _queryIsEmpty = true;
            return;
        }
        if (!_queryIsEmpty && _builder.Length > 0 && _builder[^1] != '&')
        {
            _builder.Append('&');
        }
    }
}
