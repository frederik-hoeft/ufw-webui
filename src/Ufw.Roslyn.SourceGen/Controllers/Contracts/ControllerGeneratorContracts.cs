using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Ufw.Roslyn.SourceGen.Contracts;

namespace Ufw.Roslyn.SourceGen.Controllers.Contracts;

internal sealed record ControllerGeneratorContracts(
    INamedTypeSymbol ApiControllerMappingTriggerAttribute,
    INamedTypeSymbol ApiControllerRegistrationAttribute,
    INamedTypeSymbol ControllerRouteAttribute,
    INamedTypeSymbol HttpGetRouteAttribute,
    INamedTypeSymbol HttpPostRouteAttribute,
    INamedTypeSymbol HttpPutRouteAttribute,
    INamedTypeSymbol HttpDeleteRouteAttribute,
    INamedTypeSymbol ApiEndpointMapping,
    INamedTypeSymbol ControllerActivator,
    INamedTypeSymbol IdentifiableResponse)
{
    public static bool TryResolve(Compilation compilation, SourceProductionContext context, out ControllerGeneratorContracts? contracts)
    {
        if (!GeneratorContractResolver.TryResolve(
            compilation,
            context,
            out ImmutableDictionary<ControllerGeneratorContract, INamedTypeSymbol>? registrations) ||
            registrations is null)
        {
            contracts = null;
            return false;
        }

        contracts = new ControllerGeneratorContracts(
            registrations[ControllerGeneratorContract.ApiControllerMappingTriggerAttribute],
            registrations[ControllerGeneratorContract.ApiControllerRegistrationAttribute],
            registrations[ControllerGeneratorContract.ControllerRouteAttribute],
            registrations[ControllerGeneratorContract.HttpGetRouteAttribute],
            registrations[ControllerGeneratorContract.HttpPostRouteAttribute],
            registrations[ControllerGeneratorContract.HttpPutRouteAttribute],
            registrations[ControllerGeneratorContract.HttpDeleteRouteAttribute],
            registrations[ControllerGeneratorContract.ApiEndpointMapping],
            registrations[ControllerGeneratorContract.ControllerActivator],
            registrations[ControllerGeneratorContract.IdentifiableResponse]);
        return true;
    }

    public bool TryGetHttpVerb(AttributeData attribute, out string? verb)
    {
        INamedTypeSymbol? attributeType = attribute.AttributeClass?.OriginalDefinition;
        if (SymbolEqualityComparer.Default.Equals(attributeType, HttpGetRouteAttribute.OriginalDefinition))
        {
            verb = "GET";
            return true;
        }
        if (SymbolEqualityComparer.Default.Equals(attributeType, HttpPostRouteAttribute.OriginalDefinition))
        {
            verb = "POST";
            return true;
        }
        if (SymbolEqualityComparer.Default.Equals(attributeType, HttpPutRouteAttribute.OriginalDefinition))
        {
            verb = "PUT";
            return true;
        }
        if (SymbolEqualityComparer.Default.Equals(attributeType, HttpDeleteRouteAttribute.OriginalDefinition))
        {
            verb = "DELETE";
            return true;
        }

        verb = null;
        return false;
    }
}
