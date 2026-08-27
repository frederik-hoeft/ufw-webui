using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Ufw.Roslyn.SourceGen.Json;

[Generator(LanguageNames.CSharp)]
public sealed class JsonTypeInfoBindingsGenerator : IIncrementalGenerator
{
    private const string GENERIC_JSON_TYPE_INFO_BINDINGS_ATTRIBUTE_FULL_NAME = "Ufw.Roslyn.Json.JsonTypeInfoBindingsGeneratorAttribute";
    private const string AOT_JSON_SERIALIZER_CONTEXT_FULL_NAME = "Ufw.Roslyn.Json.AotJsonSerializerContext";
    private const string JSON_SERIALIZABLE_ATTRIBUTE_FULL_NAME = "System.Text.Json.Serialization.JsonSerializableAttribute";
    private const string JSON_TYPE_INFO_FULL_NAME = "System.Text.Json.Serialization.Metadata.JsonTypeInfo";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<Model> pipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: GENERIC_JSON_TYPE_INFO_BINDINGS_ATTRIBUTE_FULL_NAME,
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, _) =>
            {
                ISymbol targetClass = context.TargetSymbol;
                ImmutableArray<AttributeData> attributes = targetClass.GetAttributes();
                AttributeData jsonTypeInfoBindingsGeneratorAttribute = attributes.FirstOrDefault(static attr => attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) is GENERIC_JSON_TYPE_INFO_BINDINGS_ATTRIBUTE_FULL_NAME)
                    ?? throw new InvalidOperationException($"{nameof(JsonTypeInfoBindingsGenerator)} requires JsonTypeInfoBindingsGeneratorAttribute to be applied to the class");
                ImmutableArray<AttributeData> jsonSerializables =
                [
                    .. attributes.Where(static attr => attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) is JSON_SERIALIZABLE_ATTRIBUTE_FULL_NAME)
                ];
                return new Model(
                    Namespace: targetClass.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)),
                    Class: targetClass,
                    GeneratorAttribute: jsonTypeInfoBindingsGeneratorAttribute,
                    JsonSerializableAttributes: jsonSerializables);
            }
        );
        context.RegisterSourceOutput(pipeline, static (context, model) =>
        {
            JsonSerializableAttributeParser parser = new(context);
            string? overrideModifier = GetOptionalOverrideModifier(model);

            StringBuilder sourceBuilder = new(
                $$"""
                #nullable enable
  
                namespace {{model.Namespace}};
 
                partial class {{model.Class.Name}}
                {
                    // the JIT will optimize this switch statement away
                    public {{overrideModifier}}global::{{JSON_TYPE_INFO_FULL_NAME}}<T>? GetTypeInfoOrDefault<T>() => (object?)null switch
                    {

                """);

            string indent = new(' ', 2 * 4);
            // 1 = Optimized, 0 = Boxed Cast
            bool useFastTypeCast = model.GeneratorAttribute.NamedArguments.Any(static arg => arg is { Key: "GenerationMode", Value.Value: 1 });

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
                    sourceBuilder.AppendLine($"global::{typeof(Unsafe).FullName}.{nameof(Unsafe.As)}<global::{JSON_TYPE_INFO_FULL_NAME}<T>>({type.Name}),");
                }
                else
                {
                    sourceBuilder.AppendLine($"(global::{JSON_TYPE_INFO_FULL_NAME}<T>)(object?){type.Name},");
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
        });
    }

    private static string? GetOptionalOverrideModifier(Model model)
    {
        // if the class inherits from AotJsonSerializerContext, then we need to generate an override for GetTypeInfoOrDefault<T>
        string? overrideModifier = null;

        // traverse the inheritance hierarchy to see if the class inherits from AotJsonSerializerContext
        for (INamedTypeSymbol? namedTypeSymbol = model.Class as INamedTypeSymbol; namedTypeSymbol is not null; namedTypeSymbol = namedTypeSymbol.BaseType)
        {
            if (namedTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)) is AOT_JSON_SERIALIZER_CONTEXT_FULL_NAME)
            {
                // include the space after the override keyword
                overrideModifier = "override ";
                break;
            }
        }

        return overrideModifier;
    }

    private sealed record Model(string Namespace, ISymbol Class, AttributeData GeneratorAttribute, ImmutableArray<AttributeData> JsonSerializableAttributes);
}
