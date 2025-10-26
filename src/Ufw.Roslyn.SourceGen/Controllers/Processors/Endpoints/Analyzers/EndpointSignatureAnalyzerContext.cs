using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints.Analyzers;

internal class EndpointSignatureAnalyzerContext
{
    public INamedTypeSymbol? ReturnType { get; set; }

    public ITypeSymbol? ResponseType { get; set; }

    public string? RequestTypeFullName { get; set; }

    public EndpointAnalyzerResult BuildEndpointMappingInfo()
    {
        _ = ReturnType ?? throw new InvalidOperationException("ReturnType must be set before building EndpointMappingInfo.");
        _ = ResponseType ?? throw new InvalidOperationException("ResponseType must be set before building EndpointMappingInfo.");
        string responseTypeFullName = ResponseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!string.IsNullOrEmpty(RequestTypeFullName))
        {
            return new EndpointAnalyzerResult(
                $"<{RequestTypeFullName}, {responseTypeFullName}>",
                ", request",
                "request, ");
        }
        return new EndpointAnalyzerResult(
            $"<{responseTypeFullName}>",
            string.Empty,
            string.Empty);
    }
}
