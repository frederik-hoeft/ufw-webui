using Microsoft.CodeAnalysis;
using System.Runtime.CompilerServices;
using Ufw.Roslyn.SourceGen.Controllers.Models;
using Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.BindingClasses;

internal sealed record BindingClassProcessorResult(ApiMappingClassInfo MappingClass, List<EndpointProcessorResult> Endpoints)
{
    public string CompilerGeneratedFullName => typeof(CompilerGeneratedAttribute).FullName!;

    public string? NamespaceName { get; } = MappingClass.ClassSymbol.ContainingNamespace?.ToDisplayString();

    public string ClassName { get; } = MappingClass.ClassSymbol.Name;

    public string MappingsFieldName { get; } = SymbolNameGenerator.MakeUnique("s_mappings");

    public string GetMappingsMethodName { get; } = "GetMappings";

    public string FactoryFullName { get; } = MappingClass.FactoryType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public string RequestEnvelopeFullName { get; } = MappingClass.RequestEnvelopeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public string ResponseEnvelopeFullName { get; } = MappingClass.ResponseEnvelopeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
