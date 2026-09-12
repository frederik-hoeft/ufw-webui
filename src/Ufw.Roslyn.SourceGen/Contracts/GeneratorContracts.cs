using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Globalization;

namespace Ufw.Roslyn.SourceGen.Contracts;

internal sealed record GeneratorContracts(
    INamedTypeSymbol ApiControllerMappingTriggerAttribute,
    INamedTypeSymbol ApiControllerRegistrationAttribute,
    INamedTypeSymbol ControllerRouteAttribute,
    INamedTypeSymbol HttpGetRouteAttribute,
    INamedTypeSymbol HttpPostRouteAttribute,
    INamedTypeSymbol HttpPutRouteAttribute,
    INamedTypeSymbol HttpDeleteRouteAttribute,
    INamedTypeSymbol ApiEndpointMapping,
    INamedTypeSymbol ControllerActivator,
    INamedTypeSymbol IdentifiableResponse,
    INamedTypeSymbol JsonTypeInfoBindingsTriggerAttribute,
    INamedTypeSymbol AotJsonSerializerContext,
    INamedTypeSymbol JsonSerializableAttribute,
    INamedTypeSymbol JsonTypeInfo)
{
    private static string RegistrationAttributeMetadataName => field ??= typeof(GeneratorContractRegistrationAttribute).FullName!;

    private static string ContractEnumMetadataName => field ??= typeof(GeneratorContract).FullName!;

    private static DiagnosticDescriptor InvalidRegistration { get; } = new(
        id: "UFWGEN001",
        title: "Invalid source-generator contract registration",
        messageFormat: "The source-generator contract registration is malformed or contains an unknown contract value",
        category: "UfwSourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static DiagnosticDescriptor DuplicateRegistration { get; } = new(
        id: "UFWGEN002",
        title: "Source-generator contract is registered more than once",
        messageFormat: "Contract '{0}' is registered by both '{1}' and '{2}'",
        category: "UfwSourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static DiagnosticDescriptor MissingRegistration { get; } = new(
        id: "UFWGEN003",
        title: "Required source-generator contract is missing",
        messageFormat: "Required source-generator contract '{0}' is not registered",
        category: "UfwSourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static bool TryResolve(Compilation compilation, SourceProductionContext context, out GeneratorContracts? contracts)
    {
        Dictionary<GeneratorContract, INamedTypeSymbol> registrations = [];
        bool foundRegistration = false;
        bool hasErrors = false;

        foreach (IAssemblySymbol assembly in EnumerateAssemblies(compilation))
        {
            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                if (!IsContractRegistration(attribute))
                {
                    continue;
                }

                foundRegistration = true;
                if (!TryReadRegistration(attribute, out GeneratorContract contract, out INamedTypeSymbol? registeredType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidRegistration,
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
                    hasErrors = true;
                    continue;
                }

                if (registrations.TryGetValue(contract, out INamedTypeSymbol? existingType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateRegistration,
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        contract,
                        existingType.ToDisplayString(),
                        registeredType!.ToDisplayString()));
                    hasErrors = true;
                    continue;
                }

                registrations.Add(contract, registeredType!);
            }
        }

        if (!foundRegistration)
        {
            contracts = null;
            return false;
        }

        foreach (GeneratorContract contract in (GeneratorContract[])Enum.GetValues(typeof(GeneratorContract)))
        {
            if (registrations.ContainsKey(contract))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(MissingRegistration, Location.None, contract));
            hasErrors = true;
        }

        if (hasErrors)
        {
            contracts = null;
            return false;
        }

        contracts = new GeneratorContracts(
            registrations[GeneratorContract.ApiControllerMappingTriggerAttribute],
            registrations[GeneratorContract.ApiControllerRegistrationAttribute],
            registrations[GeneratorContract.ControllerRouteAttribute],
            registrations[GeneratorContract.HttpGetRouteAttribute],
            registrations[GeneratorContract.HttpPostRouteAttribute],
            registrations[GeneratorContract.HttpPutRouteAttribute],
            registrations[GeneratorContract.HttpDeleteRouteAttribute],
            registrations[GeneratorContract.ApiEndpointMapping],
            registrations[GeneratorContract.ControllerActivator],
            registrations[GeneratorContract.IdentifiableResponse],
            registrations[GeneratorContract.JsonTypeInfoBindingsTriggerAttribute],
            registrations[GeneratorContract.AotJsonSerializerContext],
            registrations[GeneratorContract.JsonSerializableAttribute],
            registrations[GeneratorContract.JsonTypeInfo]);
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

    private static bool IsContractRegistration(AttributeData attribute) =>
        attribute.AttributeClass?.OriginalDefinition.GetFullMetadataName() is string metadataName &&
        metadataName.Equals(RegistrationAttributeMetadataName, StringComparison.Ordinal);

    private static bool TryReadRegistration(AttributeData attribute, out GeneratorContract contract, out INamedTypeSymbol? registeredType)
    {
        contract = default;
        registeredType = null;

        if (attribute.ConstructorArguments.Length != 2 ||
            attribute.ConstructorArguments[0] is not { Type: INamedTypeSymbol contractType, Value: { } rawContractValue } ||
            !contractType.OriginalDefinition.GetFullMetadataName().Equals(ContractEnumMetadataName, StringComparison.Ordinal) ||
            attribute.ConstructorArguments[1] is not { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol type })
        {
            return false;
        }

        int contractValue = Convert.ToInt32(rawContractValue, CultureInfo.InvariantCulture);
        if (!Enum.IsDefined(typeof(GeneratorContract), contractValue))
        {
            return false;
        }

        contract = (GeneratorContract)contractValue;
        registeredType = type.OriginalDefinition;
        return true;
    }

    private static ImmutableArray<IAssemblySymbol> EnumerateAssemblies(Compilation compilation)
    {
        ImmutableArray<IAssemblySymbol>.Builder assemblies = ImmutableArray.CreateBuilder<IAssemblySymbol>();
        HashSet<IAssemblySymbol> seen = new(SymbolEqualityComparer.Default);

        if (seen.Add(compilation.Assembly))
        {
            assemblies.Add(compilation.Assembly);
        }

        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly && seen.Add(assembly))
            {
                assemblies.Add(assembly);
            }
        }

        return assemblies.ToImmutable();
    }
}
