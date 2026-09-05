namespace Ufw.Client.Auth;

internal sealed class HttpRequestReplaySnapshot
{
    private readonly byte[]? _content;
    private readonly Dictionary<string, string[]> _contentHeaders;
    private readonly Dictionary<string, string[]> _headers;
    private readonly Dictionary<string, object?> _options;
    private readonly HttpMethod _method;
    private readonly Uri? _requestUri;
    private readonly Version _version;
    private readonly HttpVersionPolicy _versionPolicy;

    private HttpRequestReplaySnapshot(
        HttpMethod method,
        Uri? requestUri,
        Version version,
        HttpVersionPolicy versionPolicy,
        Dictionary<string, string[]> headers,
        Dictionary<string, object?> options,
        byte[]? content,
        Dictionary<string, string[]> contentHeaders)
    {
        _method = method;
        _requestUri = requestUri;
        _version = version;
        _versionPolicy = versionPolicy;
        _headers = headers;
        _options = options;
        _content = content;
        _contentHeaders = contentHeaders;
    }

    public static async Task<HttpRequestReplaySnapshot> CaptureAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, string[]> headers = request.Headers.ToDictionary(
            static header => header.Key,
            static header => header.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object?> options = request.Options.ToDictionary(
            static option => option.Key,
            static option => option.Value,
            StringComparer.Ordinal);

        byte[]? content = null;
        Dictionary<string, string[]> contentHeaders = new(StringComparer.OrdinalIgnoreCase);
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
            content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            contentHeaders = request.Content.Headers.ToDictionary(
                static header => header.Key,
                static header => header.Value.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }

        return new HttpRequestReplaySnapshot(
            request.Method,
            request.RequestUri,
            request.Version,
            request.VersionPolicy,
            headers,
            options,
            content,
            contentHeaders);
    }

    public HttpRequestMessage CreateRequest()
    {
        HttpRequestMessage request = new(_method, _requestUri)
        {
            Version = _version,
            VersionPolicy = _versionPolicy,
        };

        foreach ((string name, string[] values) in _headers)
        {
            request.Headers.TryAddWithoutValidation(name, values);
        }

        foreach ((string name, object? value) in _options)
        {
            request.Options.Set(new HttpRequestOptionsKey<object?>(name), value);
        }

        if (_content is not null)
        {
            ByteArrayContent content = new(_content);
            foreach ((string name, string[] values) in _contentHeaders)
            {
                content.Headers.TryAddWithoutValidation(name, values);
            }

            request.Content = content;
        }

        return request;
    }
}
