using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace Ufw.Roslyn.Json;

public interface IJsonTypeInfoProvider
{
    JsonTypeInfo<T>? GetTypeInfoOrDefault<T>();

    JsonTypeInfo<T> GetTypeInfo<T>();

    bool TryGetTypeInfo<T>([NotNullWhen(true)] out JsonTypeInfo<T>? jsonTypeInfo);
}
