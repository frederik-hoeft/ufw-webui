using Microsoft.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Processors.BindingClasses;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints;
using Ufw.Roslyn.SourceGen.Controllers.Processors.EndpointVerbs;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Controllers;

internal sealed class ControllerProcessor(BindingClassProcessor parent)
{
    public List<EndpointProcessorResult> Process(INamedTypeSymbol controllerType)
    {
        List<EndpointProcessorResult> mappings = [];

        AttributeData? routeAttribute = controllerType.GetAttributes()
            .FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass?.OriginalDefinition, parent.Contracts.ControllerRouteAttribute.OriginalDefinition));
        string? controllerRoute = routeAttribute?.ConstructorArguments.FirstOrDefault().Value?.ToString();
        int? controllerPriority = GetPriority(routeAttribute);

        ControllerProcessingContext context = new(this, controllerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), controllerRoute, controllerPriority);

        EndpointVerbProcessor endpointVerbProcessor = new(parent.Context, parent.Contracts);
        EndpointProcessor endpointProcessor = new(parent.Context, parent.Contracts, context);
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

    private static int? GetPriority(AttributeData? routeAttribute)
    {
        if (routeAttribute is null)
        {
            return null;
        }

        KeyValuePair<string, TypedConstant> priorityArgument = routeAttribute.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Priority");
        return priorityArgument.Value.Value as int?;
    }
}
