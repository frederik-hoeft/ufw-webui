using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ufw.Ipc.Shared.Serialization.Json;
using Ufw.Roslyn.Json;

namespace Ufw.Ipc.Tests.Adapter.Serialization;

/// <summary>
/// JSON context used by the test adapter. Prefer production source-generated metadata for known IPC types,
/// and fall back to reflection so tests can introduce one-off request/response payloads without regenerating contexts.
/// </summary>
internal sealed class HybridMessageJsonSerializerContext : AotJsonSerializerContext
{
    private readonly IJsonTypeInfoResolver _resolver;
    private readonly JsonSerializerOptions _options;

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Test-only reflection fallback; production AOT path remains source-generated.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Test-only reflection fallback; tests do not publish Native AOT.")]
    public HybridMessageJsonSerializerContext() : base(options: null!)
    {
        _resolver = JsonTypeInfoResolver.Combine(MessageJsonSerializerContext.Default, new DefaultJsonTypeInfoResolver());

        // Keep resolver ownership on this field. Do not route GetTypeInfo through Options.GetTypeInfo,
        // because JsonSerializerContext associates itself with Options and that path recurses.
        _options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
            TypeInfoResolver = _resolver,
        };
    }

    public static HybridMessageJsonSerializerContext CreateDefault() => new();

    protected override JsonSerializerOptions? GeneratedSerializerOptions => _options;

    public override JsonTypeInfo? GetTypeInfo(Type type) => _resolver.GetTypeInfo(type, _options);

    public override JsonTypeInfo<T>? GetTypeInfoOrDefault<T>() =>
        _resolver.GetTypeInfo(typeof(T), _options) as JsonTypeInfo<T>;
}
