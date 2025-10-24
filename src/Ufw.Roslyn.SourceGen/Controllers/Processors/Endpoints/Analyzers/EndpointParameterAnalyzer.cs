using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Ufw.Roslyn.SourceGen.Controllers.Diagnostics;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints.Analyzers;

internal sealed class EndpointParameterAnalyzer(SourceProductionContext context) : IEndpointSignatureAnalyzer
{
    public bool TryAnalyze(IMethodSymbol method, EndpointSignatureAnalyzerContext analyzerContext)
    {
        // Check parameters: optional TRequest, required CancellationToken
        ImmutableArray<IParameterSymbol> parameters = method.Parameters;

        if (parameters.Length == 1)
        {
            // Only CancellationToken
            if (!IsCancellationToken(parameters[0].Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidMethodSignature,
                    method.Locations.FirstOrDefault(),
                    method.Name,
                    method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                return false;
            }
            return true;
        }
        else if (parameters.Length == 2)
        {
            // TRequest and CancellationToken
            if (!IsCancellationToken(parameters[1].Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.InvalidMethodSignature,
                    method.Locations.FirstOrDefault(),
                    method.Name,
                    method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
                return false;
            }

            analyzerContext.RequestTypeFullName = parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.InvalidMethodSignature,
            method.Locations.FirstOrDefault(),
            method.Name,
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        return false;
    }

    private static bool IsCancellationToken(ITypeSymbol type) => type.ToDisplayString().Equals(typeof(CancellationToken).FullName);
}
