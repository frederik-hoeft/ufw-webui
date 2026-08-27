using Microsoft.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Processors.BindingClasses;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints;
using Ufw.Roslyn.SourceGen.Controllers.Processors.EndpointVerbs;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Controllers;

internal sealed class ControllerProcessor(BindingClassProcessor parent)
{
    private const string ROUTE_ATTRIBUTE_FULL_NAME = "global::Ufw.Roslyn.Controllers.Routing.RouteAttribute";

    public List<EndpointProcessorResult> Process(INamedTypeSymbol controllerType)
    {
        List<EndpointProcessorResult> mappings = [];

        // Get controller route information
        string? controllerRoute = GetControllerRoute(controllerType);
        int? controllerPriority = GetControllerPriority(controllerType);

        ControllerProcessingContext context = new(
            this,
            controllerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            controllerRoute,
            controllerPriority);

        // Process public methods with HTTP verb attributes
        EndpointVerbProcessor endpointVerbProcessor = new(parent.Context);
        EndpointProcessor endpointProcessor = new(parent.Context, context);
        foreach (ISymbol member in controllerType.GetMembers())
        {
            if (member is not IMethodSymbol method || endpointVerbProcessor.Process(method) is not { } verb)
            {
                continue;
            }
            EndpointProcessorResult? mapping = endpointProcessor.Process(method, verb);
            if (mapping is not null)
            {
                mappings.Add(mapping);
            }
        }

        return mappings;
    }

    private static string? GetControllerRoute(INamedTypeSymbol controllerType)
    {
        AttributeData? routeAttr = controllerType.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == ROUTE_ATTRIBUTE_FULL_NAME);

        return routeAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();
    }

    private static int? GetControllerPriority(INamedTypeSymbol controllerType)
    {
        AttributeData? routeAttr = controllerType.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == ROUTE_ATTRIBUTE_FULL_NAME);

        if (routeAttr is null)
        {
            return null;
        }

        KeyValuePair<string, TypedConstant> priorityArg = routeAttr.NamedArguments
            .FirstOrDefault(arg => arg.Key == "Priority");

        return priorityArg.Value.Value as int?;
    }
}
