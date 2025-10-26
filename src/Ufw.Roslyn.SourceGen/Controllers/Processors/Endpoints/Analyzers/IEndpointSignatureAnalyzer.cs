using Microsoft.CodeAnalysis;

namespace Ufw.Roslyn.SourceGen.Controllers.Processors.Endpoints.Analyzers;

internal interface IEndpointSignatureAnalyzer
{
    bool TryAnalyze(IMethodSymbol method, EndpointSignatureAnalyzerContext analyzerContext);
}
