using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Ufw.Roslyn.SourceGen.Json.Contracts;

namespace Ufw.Roslyn.SourceGen.Json;

[Generator(LanguageNames.CSharp)]
public sealed class JsonTypeInfoBindingsGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat s_fullyQualifiedDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

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

    private static void Execute(Compilation compilation, ImmutableArray<INamedTypeSymbol> candidateClasses, SourceProductionContext context)
    {
        if (candidateClasses.IsDefaultOrEmpty || JsonGeneratorContracts.TryResolve(compilation, context, out JsonGeneratorContracts? resolvedContracts) is false || resolvedContracts is null)
        {
            return;
        }

        JsonGeneratorContracts contracts = resolvedContracts;
        HashSet<ISymbol> processedClasses = new(SymbolEqualityComparer.Default);
        foreach (INamedTypeSymbol candidateClass in candidateClasses)
        {
            if (!processedClasses.Add(candidateClass))
            {
                continue;
            }

            Model? model = TryCreateModel(candidateClass, contracts);
            if (model is not null)
            {
                Generate(context, contracts, model);
            }
        }
    }

    private static Model? TryCreateModel(INamedTypeSymbol targetClass, JsonGeneratorContracts contracts)
    {
        ImmutableArray<AttributeData> attributes = targetClass.GetAttributes();
        AttributeData? generatorAttribute = attributes.FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(
            attribute.AttributeClass?.OriginalDefinition,
            contracts.JsonTypeInfoBindingsTriggerAttribute.OriginalDefinition));
        if (generatorAttribute is null)
        {
            return null;
        }

        ImmutableArray<AttributeData> jsonSerializableAttributes =
        [
            .. attributes.Where(attribute => SymbolEqualityComparer.Default.Equals(
                attribute.AttributeClass?.OriginalDefinition,
                contracts.JsonSerializableAttribute.OriginalDefinition))
        ];

        return new Model(
            Namespace: targetClass.ContainingNamespace.ToDisplayString(s_fullyQualifiedDisplayFormat),
            Class: targetClass,
            GeneratorAttribute: generatorAttribute,
            JsonSerializableAttributes: jsonSerializableAttributes);
    }

    private static void Generate(SourceProductionContext context, JsonGeneratorContracts contracts, Model model)
    {
        JsonSerializableAttributeParser parser = new(context);
        string? overrideModifier = GetOptionalOverrideModifier(model, contracts);
        string jsonTypeInfoFullName = contracts.JsonTypeInfo.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        StringBuilder sourceBuilder = new(
            $$"""
            #nullable enable

            namespace {{model.Namespace}};

            partial class {{model.Class.Name}}
            {
                // the JIT will optimize this switch statement away
                public {{overrideModifier}}{{jsonTypeInfoFullName}}? GetTypeInfoOrDefault<T>() => (object?)null switch
                {

            """);

        string indent = new(' ', 2 * 4);
        bool useFastTypeCast = model.GeneratorAttribute.NamedArguments.Any(static argument => argument is { Key: "GenerationMode", Value.Value: 1 });

        foreach (AttributeData jsonSerializable in model.JsonSerializableAttributes)
        {
            INamedTypeSymbol? type = parser.GetTargetType(jsonSerializable);
            if (type is null)
            {
                continue;
            }
            sourceBuilder.Append(indent)
                .Append($"_ when typeof(T) == typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}) => ");
            if (useFastTypeCast)
            {
                sourceBuilder.AppendLine($"global::{typeof(Unsafe).FullName}.{nameof(Unsafe.As)}<{jsonTypeInfoFullName}>({type.Name}),");
            }
            else
            {
                sourceBuilder.AppendLine($"({jsonTypeInfoFullName})(object?){type.Name},");
            }
        }
        sourceBuilder.Append(
            """
                    _ => null,
                };
            }
            """);

        SourceText sourceText = SourceText.From(sourceBuilder.ToString(), Encoding.UTF8);
        context.AddSource($"{model.Class.Name}.JsonTypeInfoBindings.g.cs", sourceText);
    }

    private static string? GetOptionalOverrideModifier(Model model, JsonGeneratorContracts contracts)
    {
        for (INamedTypeSymbol? namedTypeSymbol = model.Class; namedTypeSymbol is not null; namedTypeSymbol = namedTypeSymbol.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(namedTypeSymbol.OriginalDefinition, contracts.AotJsonSerializerContext.OriginalDefinition))
            {
                return "override ";
            }
        }

        return null;
    }

    private sealed record Model(
        string Namespace,
        INamedTypeSymbol Class,
        AttributeData GeneratorAttribute,
        ImmutableArray<AttributeData> JsonSerializableAttributes);
}
