using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen;

internal static class SymbolExtensions
{
    public static bool EqualsFullyQualifiedType<T>(this ISymbol? symbol) =>
        symbol.EqualsFullyQualifiedType(typeof(T));

    public static bool EqualsFullyQualifiedType(this ISymbol? symbol, Type type)
    {
        ReadOnlySpan<string> fullyQualifiedNamespace = type.Namespace?.Split('.') ?? [];
        return symbol.EqualsFullyQualifiedType(fullyQualifiedNamespace, type.Name);
    }

    public static bool EqualsFullyQualifiedType(this ISymbol? symbol, ReadOnlySpan<string> fullyQualifiedNamespace, string typeName)
    {
        if (symbol is null)
        {
            return false;
        }
        if (!symbol.Name.Equals(typeName, StringComparison.Ordinal))
        {
            return false;
        }
        // ensure that namespaces are equal
        int i = fullyQualifiedNamespace.Length;
        for (INamespaceSymbol? ns = symbol.ContainingNamespace; ns is not null; ns = ns.ContainingNamespace)
        {
            if (i == 0)
            {
                // "" is the global namespace
                return string.IsNullOrEmpty(ns.Name);
            }
            if (!ns.Name.Equals(fullyQualifiedNamespace[--i], StringComparison.Ordinal))
            {
                return false;
            }
        }
        return i == 0;
    }
}