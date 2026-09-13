using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Ufw.Roslyn.SourceGen.Contracts;

namespace Ufw.Roslyn.SourceGen.Json.Contracts;

internal sealed record JsonGeneratorContracts(
    INamedTypeSymbol JsonTypeInfoBindingsTriggerAttribute,
    INamedTypeSymbol AotJsonSerializerContext,
    INamedTypeSymbol JsonSerializableAttribute,
    INamedTypeSymbol JsonTypeInfo)
{
    public static bool TryResolve(Compilation compilation, SourceProductionContext context, out JsonGeneratorContracts? contracts)
    {
        if (!GeneratorContractResolver.TryResolve(
            compilation,
            context,
            out ImmutableDictionary<JsonGeneratorContract, INamedTypeSymbol>? registrations) ||
            registrations is null)
        {
            contracts = null;
            return false;
        }

        contracts = new JsonGeneratorContracts(
            registrations[JsonGeneratorContract.JsonTypeInfoBindingsTriggerAttribute],
            registrations[JsonGeneratorContract.AotJsonSerializerContext],
            registrations[JsonGeneratorContract.JsonSerializableAttribute],
            registrations[JsonGeneratorContract.JsonTypeInfo]);
        return true;
    }
}
