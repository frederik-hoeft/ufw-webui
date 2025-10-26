using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Ufw.Roslyn.Json;

public abstract class AotJsonSerializerContext : JsonSerializerContext, IJsonTypeInfoProvider
{
    protected AotJsonSerializerContext() : base(null)
    {
    }

    protected AotJsonSerializerContext(JsonSerializerOptions options) : base(options)
    {
    }

    public abstract JsonTypeInfo<T>? GetTypeInfoOrDefault<T>();

    public virtual JsonTypeInfo<T> GetTypeInfo<T>() => GetTypeInfoOrDefault<T>() ?? throw new NotSupportedException($"Type {typeof(T)} was not registered with this {"JsonSerializerContext"}. To register the type, add [{"JsonSerializableAttribute"}(typeof({typeof(T).Name}))] to the definition of {GetType().Name}.");

    public virtual bool TryGetTypeInfo<T>([NotNullWhen(true)] out JsonTypeInfo<T>? jsonTypeInfo)
    {
        jsonTypeInfo = GetTypeInfoOrDefault<T>();
        return jsonTypeInfo != null;
    }
}
