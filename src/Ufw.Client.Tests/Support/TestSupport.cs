using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Ufw.Client.Auth;

namespace Ufw.Client.Tests.Support;

internal sealed class PassthroughStringLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name);

    public LocalizedString this[string name, params object[] arguments] =>
        new(name, string.Format(System.Globalization.CultureInfo.CurrentCulture, name + ":" + string.Join(",", arguments), arguments));

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;
    private long _timestamp;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override long GetTimestamp() => _timestamp;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan duration)
    {
        _utcNow += duration;
        _timestamp += duration.Ticks;
    }
}

internal sealed class InlineAuthenticationOperationCoordinator : IAuthenticationOperationCoordinator
{
    public int InvocationCount { get; private set; }

    public Task RunExclusiveAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        return operation(cancellationToken);
    }

    public Task<T> RunExclusiveAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        return operation(cancellationToken);
    }
}

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager(string uri = "https://localhost/app") => Initialize("https://localhost/", uri);

    public string? LastUri { get; private set; }

    public bool LastForceLoad { get; private set; }

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        LastUri = ToAbsoluteUri(uri).ToString();
        LastForceLoad = options.ForceLoad;
    }
}

internal sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) : HttpMessageHandler
{
    private int _callCount;

    public List<RecordedRequest> Requests { get; } = [];

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? content = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            content,
            request.Headers.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            request.Options.ToDictionary(static option => option.Key, static option => option.Value, StringComparer.Ordinal)));
        return responder(request, ++_callCount);
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? AuthorizationScheme,
    string? AuthorizationParameter,
    byte[]? Content,
    IReadOnlyDictionary<string, string[]> Headers,
    IReadOnlyDictionary<string, object?> Options);

internal sealed class RecordingJsRuntime : Microsoft.JSInterop.IJSRuntime
{
    public RecordingJsObjectReference Module { get; } = new();

    public Exception? ImportException { get; set; }

    public int ImportCount { get; private set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier != "import")
        {
            throw new InvalidOperationException($"Unexpected JS runtime invocation '{identifier}'.");
        }

        ImportCount++;
        if (ImportException is not null)
        {
            return ValueTask.FromException<TValue>(ImportException);
        }

        return ValueTask.FromResult((TValue)(object)Module);
    }
}

internal sealed class RecordingJsObjectReference : Microsoft.JSInterop.IJSObjectReference
{
    private readonly Dictionary<string, object?> _results = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> _exceptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _delays = new(StringComparer.Ordinal);

    public List<(string Identifier, object?[] Args)> Calls { get; } = [];

    public int DisposeCount { get; private set; }

    public void SetResult(string identifier, object? value) => _results[identifier] = value;

    public void SetException(string identifier, Exception exception) => _exceptions[identifier] = exception;

    public void SetDelay(string identifier, Task delay) => _delays[identifier] = delay;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Calls.Add((identifier, args ?? []));
        if (_exceptions.TryGetValue(identifier, out Exception? exception))
        {
            return ValueTask.FromException<TValue>(exception);
        }

        if (_delays.TryGetValue(identifier, out Task? delay))
        {
            return new ValueTask<TValue>(WaitAndReturnAsync<TValue>(delay, cancellationToken));
        }

        if (_results.TryGetValue(identifier, out object? result))
        {
            return ValueTask.FromResult((TValue)result!);
        }

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }

    private static async Task<TValue> WaitAndReturnAsync<TValue>(Task delay, CancellationToken cancellationToken)
    {
        await delay.WaitAsync(cancellationToken);
        return default!;
    }
}
