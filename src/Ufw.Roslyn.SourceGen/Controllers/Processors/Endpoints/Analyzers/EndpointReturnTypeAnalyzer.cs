using Microsoft.CodeAnalysis;
using Ufw.Roslyn.SourceGen.Controllers.Contracts;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints.Analyzers;

internal sealed class EndpointReturnTypeAnalyzer(SourceProductionContext context, ControllerGeneratorContracts contracts) : IEndpointSignatureAnalyzer
{
    public bool TryAnalyze(IMethodSymbol method, EndpointSignatureAnalyzerContext analyzerContext)
    {
        if (method.ReturnType is not INamedTypeSymbol { IsGenericType: true } returnType || !IsValueTask(returnType.ConstructUnboundGenericType()))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidReturnType,
                method.Locations.FirstOrDefault(),
                method.Name,
                method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            return false;
        }
        analyzerContext.ReturnType = returnType;
        ITypeSymbol? responseType = returnType.TypeArguments.FirstOrDefault();
        if (responseType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.InvalidReturnType,
                method.Locations.FirstOrDefault(),
                method.Name,
                method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
            return false;
        }
        analyzerContext.ResponseType = responseType;
        while (responseType is INamedTypeSymbol namedType)
        {
            if (namedType.AllInterfaces.Any(interfaceType => SymbolEqualityComparer.Default.Equals(
                interfaceType.OriginalDefinition,
                contracts.IdentifiableResponse.OriginalDefinition)))
            {
                return true;
            }
            responseType = namedType.BaseType;
        }
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ReturnTypeMustImplementIdentifiable,
            method.Locations.FirstOrDefault(),
            method.Name,
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        return false;
    }

    private static bool IsValueTask(INamedTypeSymbol type)
    {
        string valueTaskFullName = $"{typeof(ValueTask<>).Namespace}.{nameof(ValueTask)}<>";
        return type.ToDisplayString().Equals(valueTaskFullName);
    }
}
