using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Ufw.Roslyn.SourceGen.Contracts;

internal static class GeneratorContractResolver
{
    private static string RegistrationAttributeMetadataName => field ??= typeof(GeneratorContractRegistrationAttribute<>).FullName!;

    private static DiagnosticDescriptor InvalidRegistration { get; } = new(
        id: "UFWGEN001",
        title: "Invalid source-generator contract registration",
        messageFormat: "The source-generator contract registration for family '{0}' is malformed or contains an unknown contract value",
        category: "UfwSourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static DiagnosticDescriptor DuplicateRegistration { get; } = new(
        id: "UFWGEN002",
        title: "Source-generator contract is registered more than once",
        messageFormat: "Contract '{0}.{1}' is registered by both '{2}' and '{3}'",
        category: "UfwSourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static DiagnosticDescriptor MissingRegistration { get; } = new(
        id: "UFWGEN003",
        title: "Required source-generator contract is missing",
        messageFormat: "Required source-generator contract '{0}.{1}' is not registered",
        category: "UfwSourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static bool TryResolve<TContract>(Compilation compilation, SourceProductionContext context, out ImmutableDictionary<TContract, INamedTypeSymbol>? registrations)
        where TContract : struct, Enum
    {
        Dictionary<TContract, INamedTypeSymbol> discoveredRegistrations = [];
        bool foundRegistration = false;
        bool hasErrors = false;
        string contractFamilyName = typeof(TContract).Name;

        foreach (IAssemblySymbol assembly in EnumerateAssemblies(compilation))
        {
            foreach (AttributeData attribute in assembly.GetAttributes())
            {
                if (!IsContractRegistration<TContract>(attribute))
                {
                    continue;
                }

                foundRegistration = true;
                if (!TryReadRegistration(attribute, out TContract contract, out INamedTypeSymbol? registeredType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidRegistration,
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        contractFamilyName));
                    hasErrors = true;
                    continue;
                }

                if (discoveredRegistrations.TryGetValue(contract, out INamedTypeSymbol? existingType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateRegistration,
                        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                        contractFamilyName,
                        contract,
                        existingType.ToDisplayString(),
                        registeredType!.ToDisplayString()));
                    hasErrors = true;
                    continue;
                }

                discoveredRegistrations.Add(contract, registeredType!);
            }
        }

        if (!foundRegistration)
        {
            registrations = null;
            return false;
        }

        foreach (TContract contract in (TContract[])Enum.GetValues(typeof(TContract)))
        {
            if (discoveredRegistrations.ContainsKey(contract))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(MissingRegistration, Location.None, contractFamilyName, contract));
            hasErrors = true;
        }

        if (hasErrors)
        {
            registrations = null;
            return false;
        }

        registrations = discoveredRegistrations.ToImmutableDictionary();
        return true;
    }

    private static bool IsContractRegistration<TContract>(AttributeData attribute) where TContract : struct, Enum
    {
        INamedTypeSymbol? attributeType = attribute.AttributeClass;
        return attributeType is not null
            && attributeType.OriginalDefinition.GetFullMetadataName().Equals(RegistrationAttributeMetadataName, StringComparison.Ordinal)
            && attributeType.TypeArguments is [INamedTypeSymbol contractType]
            && contractType.OriginalDefinition.GetFullMetadataName().Equals(typeof(TContract).FullName, StringComparison.Ordinal);
    }

    private static bool TryReadRegistration<TContract>(AttributeData attribute, out TContract contract, out INamedTypeSymbol? registeredType)
        where TContract : struct, Enum
    {
        contract = default;
        registeredType = null;

        if (attribute.ConstructorArguments.Length != 2
            || attribute.ConstructorArguments[0].Value is not { } rawContractValue
            || attribute.ConstructorArguments[1] is not { Kind: TypedConstantKind.Type, Value: INamedTypeSymbol type })
        {
            return false;
        }

        object enumValue = Enum.ToObject(typeof(TContract), rawContractValue);
        if (!Enum.IsDefined(typeof(TContract), enumValue))
        {
            return false;
        }

        contract = (TContract)enumValue;
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
