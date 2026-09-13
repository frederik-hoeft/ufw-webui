using Microsoft.CodeAnalysis.Text;
using System.Reflection;
using System.Text;

namespace Ufw.Roslyn.SourceGen.Contracts;

internal static class EmbeddedSourceText
{
    public static SourceText FromType<T>() => FromType(typeof(T));

    public static SourceText FromType(Type type)
    {
        Assembly assembly = typeof(EmbeddedSourceText).Assembly;
        if (type.Assembly != assembly)
        {
            throw new InvalidOperationException($"The canonical type '{type}' must be defined in the source-generator assembly.");
        }

        string typeName = type.FullName ?? throw new InvalidOperationException($"The canonical type '{type}' does not have a full metadata name.");
        string resourceName = $"{typeName}.cs";
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The canonical source resource '{resourceName}' was not found.");
        return SourceText.From(stream, Encoding.UTF8, canBeEmbedded: true);
    }
}
