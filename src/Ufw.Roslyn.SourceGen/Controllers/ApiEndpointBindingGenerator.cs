using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using Ufw.Roslyn.SourceGen.Contracts;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;
using Ufw.Roslyn.SourceGen.Controllers.Emitters;
using Ufw.Roslyn.SourceGen.Controllers.Models;
using Ufw.Roslyn.SourceGen.Controllers.Processors.BindingClasses;

namespace Ufw.Roslyn.SourceGen.Controllers;

[Generator(LanguageNames.CSharp)]
public sealed class ApiEndpointBindingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<INamedTypeSymbol> candidateClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (generatorContext, _) => GetCandidateClass(generatorContext))
            .Where(static classSymbol => classSymbol is not null)
            .Select(static (classSymbol, _) => classSymbol!);

        IncrementalValueProvider<(Compilation, ImmutableArray<INamedTypeSymbol>)> compilationAndClasses =
            context.CompilationProvider.Combine(candidateClasses.Collect());

        context.RegisterSourceOutput(compilationAndClasses, static (sourceContext, source) =>
            Execute(source.Item1, source.Item2, sourceContext));
    }

    private static INamedTypeSymbol? GetCandidateClass(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        return context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
    }

    private static ApiMappingClassInfo? GetApiMappingClassInfo(INamedTypeSymbol classSymbol, GeneratorContracts contracts)
    {
        AttributeData? mappingGeneratorAttribute = null;
        List<INamedTypeSymbol> controllerRegistrations = [];

        foreach (AttributeData attribute in classSymbol.GetAttributes())
        {
            INamedTypeSymbol? attributeType = attribute.AttributeClass?.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(attributeType, contracts.ApiControllerMappingTriggerAttribute.OriginalDefinition))
            {
                mappingGeneratorAttribute = attribute;
            }
            else if (SymbolEqualityComparer.Default.Equals(attributeType, contracts.ApiControllerRegistrationAttribute.OriginalDefinition) &&
                attribute.AttributeClass?.TypeArguments.FirstOrDefault() is INamedTypeSymbol controllerType)
            {
                controllerRegistrations.Add(controllerType);
            }
        }

        if (mappingGeneratorAttribute?.AttributeClass is not INamedTypeSymbol mappingGeneratorAttributeType ||
            mappingGeneratorAttributeType.TypeArguments.ElementAtOrDefault(0) is not INamedTypeSymbol factoryType ||
            mappingGeneratorAttributeType.TypeArguments.ElementAtOrDefault(1) is not INamedTypeSymbol requestEnvelopeType ||
            mappingGeneratorAttributeType.TypeArguments.ElementAtOrDefault(2) is not INamedTypeSymbol responseEnvelopeType)
        {
            return null;
        }

        return new ApiMappingClassInfo(classSymbol, factoryType, requestEnvelopeType, responseEnvelopeType, [.. controllerRegistrations]);
    }

    private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol> candidateClasses, SourceProductionContext context)
    {
        if (candidateClasses.IsDefaultOrEmpty || GeneratorContracts.TryResolve(compilation, context, out GeneratorContracts? resolvedContracts) is false || resolvedContracts is null)
        {
            return;
        }

        GeneratorContracts contracts = resolvedContracts;
        HashSet<ISymbol> processedClasses = new(SymbolEqualityComparer.Default);
        MappingClassEmitter emitter = new(context, contracts);
        foreach (INamedTypeSymbol candidateClass in candidateClasses)
        {
            if (!processedClasses.Add(candidateClass))
            {
                continue;
            }

            ApiMappingClassInfo? mappingClass = GetApiMappingClassInfo(candidateClass, contracts);
            if (mappingClass is null)
            {
                continue;
            }

            if (mappingClass.ControllerRegistrations.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingControllerRegistrations,
                    mappingClass.ClassSymbol.Locations.FirstOrDefault(),
                    mappingClass.ClassSymbol.Name));
            }

            BindingClassProcessor mappingClassProcessor = new(context, contracts, mappingClass);
            BindingClassProcessorResult result = mappingClassProcessor.Process();
            emitter.Emit(result);
        }
    }
}
