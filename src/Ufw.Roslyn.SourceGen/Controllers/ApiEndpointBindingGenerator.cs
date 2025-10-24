using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;
using Ufw.Roslyn.SourceGen.Controllers.Emitters;
using Ufw.Roslyn.SourceGen.Controllers.Models;
using Ufw.Roslyn.SourceGen.Controllers.Processors.BindingClasses;

namespace Ufw.Roslyn.SourceGen.Controllers;

[Generator(LanguageNames.CSharp)]
public sealed class ApiEndpointBindingGenerator : IIncrementalGenerator
{
    private const string API_CONTROLLER_MAPPING_GENERATOR_ATTRIBUTE_FULL_NAME = "global::Ufw.Roslyn.Controllers.Mapping.Attributes.ApiControllerMappingGeneratorAttribute<,,>";
    private const string API_CONTROLLER_MAPPING_GENERATOR_ATTRIBUTE_NAME = "ApiControllerMappingGenerator";
    private const string API_CONTROLLER_REGISTRATION_ATTRIBUTE_FULL_NAME = "global::Ufw.Roslyn.Controllers.Mapping.Attributes.ApiControllerRegistrationAttribute<>";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find classes decorated with ApiControllerMappingGenerator attribute
        IncrementalValuesProvider<ApiMappingClassInfo> apiMappingClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetApiMappingClassInfo(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        // Combine with compilation for symbol information
        IncrementalValueProvider<(Compilation, ImmutableArray<ApiMappingClassInfo>)> compilationAndClasses = 
            context.CompilationProvider.Combine(apiMappingClasses.Collect());

        context.RegisterSourceOutput(compilationAndClasses, (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } classDecl &&
               classDecl.AttributeLists
                   .SelectMany(al => al.Attributes)
                   .Any(attr => attr.Name.ToString().Contains(API_CONTROLLER_MAPPING_GENERATOR_ATTRIBUTE_NAME));
    }

    private static ApiMappingClassInfo? GetApiMappingClassInfo(GeneratorSyntaxContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDecl)
        {
            return null;
        }

        SemanticModel semanticModel = context.SemanticModel;
        INamedTypeSymbol? classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol is null)
        {
            return null;
        }

        // Find ApiControllerMappingGenerator attribute
        AttributeData? mappingGeneratorAttr = null;
        List<INamedTypeSymbol> controllerRegistrations = [];

        foreach (AttributeData attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass is not { IsGenericType: true })
            {
                continue;
            }
            string fullyQualifiedName = attribute.AttributeClass.ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (fullyQualifiedName is API_CONTROLLER_MAPPING_GENERATOR_ATTRIBUTE_FULL_NAME)
            {
                mappingGeneratorAttr = attribute;
            }
            else if (fullyQualifiedName is API_CONTROLLER_REGISTRATION_ATTRIBUTE_FULL_NAME)
            {
                if (attribute.AttributeClass?.TypeArguments.FirstOrDefault() is INamedTypeSymbol controllerType)
                {
                    controllerRegistrations.Add(controllerType);
                }
            }
        }

        if (mappingGeneratorAttr is null)
        {
            return null;
        }

        // Extract factory type and envelope types from the generic attribute
        if (mappingGeneratorAttr.AttributeClass?.TypeArguments.ElementAtOrDefault(0) is not INamedTypeSymbol factoryType ||
            mappingGeneratorAttr.AttributeClass?.TypeArguments.ElementAtOrDefault(1) is not INamedTypeSymbol requestEnvelopeType ||
            mappingGeneratorAttr.AttributeClass?.TypeArguments.ElementAtOrDefault(2) is not INamedTypeSymbol responseEnvelopeType)
        {
            return null;
        }

        return new ApiMappingClassInfo(
            classSymbol,
            factoryType,
            requestEnvelopeType,
            responseEnvelopeType,
            [.. controllerRegistrations]);
    }

    private static void Execute(Compilation compilation, ImmutableArray<ApiMappingClassInfo> mappingClasses, SourceProductionContext context)
    {
        MappingClassEmitter emitter = new(context);
        foreach (ApiMappingClassInfo mappingClass in mappingClasses)
        {
            if (mappingClass.ControllerRegistrations.Length == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingControllerRegistrations,
                    mappingClass.ClassSymbol.Locations.FirstOrDefault(),
                    mappingClass.ClassSymbol.Name));
            }
            BindingClassProcessor mappingClassProcessor = new(context, compilation, mappingClass);
            BindingClassProcessorResult result = mappingClassProcessor.Process();
            emitter.Emit(result);
        }
    }
}
