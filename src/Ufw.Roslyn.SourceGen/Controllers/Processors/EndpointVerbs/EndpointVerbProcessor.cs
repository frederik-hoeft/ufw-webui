using Microsoft.CodeAnalysis;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.EndpointVerbs;

internal sealed class EndpointVerbProcessor(SourceProductionContext context)
{
    private static readonly FrozenDictionary<string, string> s_verbAttributeMap = new Dictionary<string, string>
    {
        { "global::Ufw.Roslyn.Controllers.Routing.GetAttribute", "GET" },
        { "global::Ufw.Roslyn.Controllers.Routing.PostAttribute", "POST" },
        { "global::Ufw.Roslyn.Controllers.Routing.PutAttribute", "PUT" },
        { "global::Ufw.Roslyn.Controllers.Routing.DeleteAttribute", "DELETE" }
    }.ToFrozenDictionary();

    private static bool TryGetVerbFromAttribute(AttributeData attribute, [NotNullWhen(true)] out string? verb)
    {
        if (attribute.AttributeClass is null)
        {
            verb = null;
            return false;
        }
        string fullyQualifiedName = attribute.AttributeClass.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return s_verbAttributeMap.TryGetValue(fullyQualifiedName, out verb);
    }

    public EndpointVerbProcessorResult? Process(IMethodSymbol method)
    {
        ImmutableArray<AttributeData> attributes = method.GetAttributes();
        AttributeData? verbAttribute = null;
        foreach (AttributeData attribute in attributes)
        {
            if (TryGetVerbFromAttribute(attribute, out _))
            {
                if (verbAttribute is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MultipleHttpVerbAttributes,
                        method.Locations.FirstOrDefault(),
                        method.Name));
                    return null;
                }
                verbAttribute = attribute;
            }
        }
        if (verbAttribute is null)
        {
            return null;
        }

        if (method.DeclaredAccessibility != Accessibility.Public || method.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidMethodVisibility,
                method.Locations.FirstOrDefault(),
                method.Name));
            return null;
        }

        bool success = TryGetVerbFromAttribute(verbAttribute, out string? verb);
        Debug.Assert(success && verb is not null);
        string? methodRoute = GetMethodRoute(verbAttribute);
        int? methodPriority = GetMethodPriority(verbAttribute);

        return new EndpointVerbProcessorResult(
            verbAttribute,
            verb!,
            methodRoute,
            methodPriority);
    }

    private static string? GetMethodRoute(AttributeData httpAttr) => httpAttr.ConstructorArguments.FirstOrDefault().Value?.ToString();

    private static int? GetMethodPriority(AttributeData httpAttr)
    {
        KeyValuePair<string, TypedConstant> priorityArg = httpAttr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Priority");

        return priorityArg.Value.Value as int?;
    }
}
