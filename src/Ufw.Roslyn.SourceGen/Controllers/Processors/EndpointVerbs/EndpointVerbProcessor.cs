using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Contracts;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.EndpointVerbs;

internal sealed class EndpointVerbProcessor(SourceProductionContext context, ControllerGeneratorContracts contracts)
{
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
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MultipleHttpVerbAttributes, method.Locations.FirstOrDefault(), method.Name));
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
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidMethodVisibility, method.Locations.FirstOrDefault(), method.Name));
            return null;
        }

        bool success = TryGetVerbFromAttribute(verbAttribute, out string? verb);
        Debug.Assert(success && verb is not null);
        string? methodRoute = GetMethodRoute(verbAttribute);
        int? methodPriority = GetMethodPriority(verbAttribute);

        return new EndpointVerbProcessorResult(verbAttribute, verb!, methodRoute, methodPriority);
    }

    private bool TryGetVerbFromAttribute(AttributeData attribute, [NotNullWhen(true)] out string? verb) =>
        contracts.TryGetHttpVerb(attribute, out verb);

    private static string? GetMethodRoute(AttributeData httpAttribute) => httpAttribute.ConstructorArguments.FirstOrDefault().Value?.ToString();

    private static int? GetMethodPriority(AttributeData httpAttribute)
    {
        KeyValuePair<string, TypedConstant> priorityArgument = httpAttribute.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Priority");
        return priorityArgument.Value.Value as int?;
    }
}
